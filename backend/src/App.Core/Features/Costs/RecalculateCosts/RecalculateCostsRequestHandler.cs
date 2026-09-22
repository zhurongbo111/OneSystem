using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Costs.RecalculateCosts;

/// <summary>
/// 成本重算 / 初始化用例（erp-cost design §3.4）：按流水时间顺序重放全部变动，
/// 逐条按 §0.2 的来源表解析成本单价，重算流水的 <c>UnitCost</c> / <c>TotalCost</c> 并写回库存 <c>CostAmount</c> / <c>AverageCost</c>。
/// **幂等**：完全按流水推演、与当前成本列无关，可反复执行；**不改变任何 <c>Quantity</c>**
/// （<c>Σ 流水 Quantity == Inventory.Quantity</c> 不受影响）。
/// 并发（同一进程内）拒绝：<c>40118</c>。
/// </summary>
public sealed class RecalculateCostsRequestHandler : IRequestHandler<RecalculateCostsRequest, CostRecalculateResultDto>
{
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CostRecalculationLock _recalculationLock;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化成本重算用例处理器
    /// </summary>
    /// <param name="stockMovementRepository">库存流水仓储</param>
    /// <param name="inventoryRepository">库存台账仓储</param>
    /// <param name="unitOfWork">工作单元（重算写回同一事务）</param>
    /// <param name="recalculationLock">重算互斥锁（Singleton）</param>
    /// <param name="auditLogger">操作日志写入器（重算无明确业务对象，故 ResourceId 为空）</param>
    public RecalculateCostsRequestHandler(
        IStockMovementRepository stockMovementRepository,
        IInventoryRepository inventoryRepository,
        IUnitOfWork unitOfWork,
        CostRecalculationLock recalculationLock,
        IAuditLogger auditLogger)
    {
        _stockMovementRepository = stockMovementRepository;
        _inventoryRepository = inventoryRepository;
        _unitOfWork = unitOfWork;
        _recalculationLock = recalculationLock;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理成本重算请求
    /// </summary>
    /// <param name="request">重算请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<CostRecalculateResultDto> HandleAsync(
        RecalculateCostsRequest request, CancellationToken cancellationToken = default)
    {
        if (!_recalculationLock.TryEnter())
        {
            throw new BusinessException(ErrorCode.CostRecalculationRunning, "成本重算正在进行，请稍后重试");
        }

        try
        {
            // 取全部流水（按 CreatedAt, Id 升序）推演；期间参数只决定「写回哪些流水」，不切断开局结存
            var rows = await _stockMovementRepository.GetAllForCostAsync(request.ProductId, null, null, cancellationToken);

            var states = new Dictionary<Guid, CostState>();

            // 本次重算已推演出的成本单价（来源单据 + 商品 + 变动类型 → 单价）：
            // 冲销类优先复用「本次推演」的结果，而不是历史成本列 —— 重算与当前成本列无关才谈得上幂等
            var resolvedCosts = new Dictionary<(Guid SourceId, Guid ProductId, StockMovementType Type), decimal>();

            var updates = new List<(Guid Id, decimal UnitCost, decimal TotalCost)>();
            var missingCostCount = 0;

            foreach (var row in rows)
            {
                if (!states.TryGetValue(row.ProductId, out var state))
                {
                    state = new CostState();
                    states[row.ProductId] = state;
                }

                var (unitCost, missing) = await ResolveUnitCostAsync(row, state, resolvedCosts, cancellationToken);
                resolvedCosts[(row.SourceId ?? Guid.Empty, row.ProductId, row.MovementType)] = unitCost;

                if (missing)
                {
                    missingCostCount++;
                }

                var absQuantity = Math.Abs(row.Quantity);
                var totalCost = CostCalculator.TotalCost(absQuantity, unitCost);

                if (IsInRange(row, request.Start, request.End))
                {
                    updates.Add((row.Id, unitCost, row.Quantity > 0 ? totalCost : -totalCost));
                }

                // 推演结存：入库加数量加金额（先加数量再加金额），出库减数量减金额且均价不变；
                // 数量归零时金额归零以消除尾差、均价保留（供后续入库前展示与兜底）
                if (row.Quantity > 0)
                {
                    state.Quantity += absQuantity;
                    state.Amount += totalCost;
                }
                else
                {
                    state.Quantity -= absQuantity;
                    state.Amount -= totalCost;
                }

                if (state.Quantity == 0)
                {
                    state.Amount = 0m;
                }
                else
                {
                    state.AverageCost = CostCalculator.AverageCost(state.Amount, state.Quantity);
                }
            }

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                foreach (var update in updates)
                {
                    await _stockMovementRepository.UpdateCostAsync(
                        update.Id, update.UnitCost, update.TotalCost, cancellationToken);
                }

                foreach (var pair in states)
                {
                    await _inventoryRepository.SetCostAsync(
                        pair.Key, pair.Value.Amount, pair.Value.AverageCost, cancellationToken);
                }

                var changeBuilder = new AuditChangeBuilder()
                    .Add("start", "重算起始日期", null, AuditSummary.Date(request.Start))
                    .Add("end", "重算结束日期", null, AuditSummary.Date(request.End))
                    .Add("movementCount", "重算流水数", null, AuditSummary.Count(updates.Count))
                    .Add("productCount", "涉及商品数", null, AuditSummary.Count(states.Count))
                    .Add("missingCostCount", "缺价流水数", null, AuditSummary.Count(missingCostCount));
                await _auditLogger.RecordAsync(new AuditEntry
                {
                    Resource = AuditResource.Cost,
                    Action = AuditAction.Recalculate,
                    Summary = $"成本重算 {AuditSummary.Date(request.Start)} ~ {AuditSummary.Date(request.End)}：{AuditSummary.Count(updates.Count)} 条流水、{AuditSummary.Count(states.Count)} 个商品、缺价 {AuditSummary.Count(missingCostCount)} 条",
                    Changes = changeBuilder.Build(),
                    ChangesTruncated = changeBuilder.Truncated,
                    UtcNow = DateTimeOffset.UtcNow,
                }, cancellationToken);

                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }

            return new CostRecalculateResultDto
            {
                MovementCount = updates.Count,
                MissingCostCount = missingCostCount,
                ProductCount = states.Count,
            };
        }
        finally
        {
            _recalculationLock.Release();
        }
    }

    /// <summary>
    /// 按 design §0.2 的来源表解析本次变动的成本单价；第二项为是否缺价（单价无法推算）。
    /// 原则：冲销类一律复用原方向流水的成本单价（保证「入 + 冲回 = 0」），新增交易类按均价或录入价。
    /// </summary>
    private async Task<(decimal UnitCost, bool Missing)> ResolveUnitCostAsync(
        StockMovementCostRow row,
        CostState state,
        Dictionary<(Guid SourceId, Guid ProductId, StockMovementType Type), decimal> resolvedCosts,
        CancellationToken cancellationToken)
    {
        switch (row.MovementType)
        {
            case StockMovementType.PurchaseInbound:
                return row.PurchaseUnitPrice is { } purchasePrice ? (purchasePrice, false) : (0m, true);

            case StockMovementType.InitialStock:
                return row.InitialUnitCost is { } initialCost ? (initialCost, false) : (0m, true);

            case StockMovementType.PurchaseVoid:
                return await ResolveReversalAsync(row, StockMovementType.PurchaseInbound, resolvedCosts, cancellationToken);

            case StockMovementType.SalesVoid:
                return await ResolveReversalAsync(row, StockMovementType.SalesOutbound, resolvedCosts, cancellationToken);

            case StockMovementType.PurchaseReturnVoid:
                return await ResolveReversalAsync(row, StockMovementType.PurchaseReturnOut, resolvedCosts, cancellationToken);

            case StockMovementType.SalesReturnVoid:
                return await ResolveReversalAsync(row, StockMovementType.SalesReturnIn, resolvedCosts, cancellationToken);

            case StockMovementType.SalesReturnIn:
                // 优先按被退销售单的原出库成本退回；销售退货不关联原单（specs/021 §5），查不到兜底当前均价
                if (row.SourceId is null)
                {
                    return (state.AverageCost, false);
                }

                var salesCost = await _stockMovementRepository.GetMovementUnitCostAsync(
                    row.SourceId.Value, row.ProductId, StockMovementType.SalesOutbound, cancellationToken);
                return salesCost is not null ? (salesCost.Value, false) : (state.AverageCost, false);

            default:
                // SalesOutbound / StockTakeAdjust / PurchaseReturnOut：按变动前均价
                return (state.AverageCost, false);
        }
    }

    /// <summary>
    /// 冲销类成本还原：**优先复用本次重算已推演出的原方向单价**（保证重算与历史成本列无关），
    /// 未推演到（如原流水在更早的期间之外）时才回查库中的原流水成本；都查不到按 0 并计缺价。
    /// </summary>
    private async Task<(decimal UnitCost, bool Missing)> ResolveReversalAsync(
        StockMovementCostRow row,
        StockMovementType originalType,
        Dictionary<(Guid SourceId, Guid ProductId, StockMovementType Type), decimal> resolvedCosts,
        CancellationToken cancellationToken)
    {
        if (row.SourceId is null)
        {
            return (0m, true);
        }

        if (resolvedCosts.TryGetValue((row.SourceId.Value, row.ProductId, originalType), out var resolved))
        {
            return (resolved, false);
        }

        var unitCost = await _stockMovementRepository.GetMovementUnitCostAsync(
            row.SourceId.Value, row.ProductId, originalType, cancellationToken);
        return unitCost is not null ? (unitCost.Value, false) : (0m, true);
    }

    /// <summary>判断流水是否落在重算期间内（半开区间；两端可空表示不限）</summary>
    private static bool IsInRange(StockMovementCostRow row, DateTimeOffset? start, DateTimeOffset? end)
        => (start is null || row.CreatedAt >= start) && (end is null || row.CreatedAt < end);

    /// <summary>重算过程中的商品结存推演状态（数量 / 金额 / 均价）</summary>
    private sealed class CostState
    {
        /// <summary>推演中的结存数量</summary>
        public int Quantity { get; set; }

        /// <summary>推演中的结存成本额</summary>
        public decimal Amount { get; set; }

        /// <summary>推演中的移动加权平均单价（结存为 0 时保留最后值）</summary>
        public decimal AverageCost { get; set; }
    }
}
