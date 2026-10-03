using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Batches;
using App.Core.Features.Warehouses;

namespace App.Core.Features.StockTakes.CreateStockTake;

/// <summary>
/// 新增盘点 / 期初建账单用例（一步式：保存即生效）：
/// 商品逐行校验（存在 / 启用）+ 期初限制（无库存变动）→ 单号生成（ST + yyyyMMdd + 序号，唯一索引冲突重试最多 3 次）
/// → 同一事务：读账面（GetQuantitiesAsync，事务内读）+ 重算差异 + 插单明细 + 逐差异行库存设定（SetQuantityAsync）+ 写流水
/// （IUnitOfWork 包裹）。差异行（Difference != 0）才改库存与写流水；无差异行只落明细。
/// </summary>
public sealed class CreateStockTakeRequestHandler : IRequestHandler<CreateStockTakeRequest, StockTakeDetailDto>
{
    /// <summary>单号冲突重试上限（含首次）；单号前缀 ST 固化在仓储 GenerateTakeNoAsync 内（见 design.md §2.1）</summary>
    private const int MaxTakeNoAttempts = 3;

    /// <summary>摘要中列举的商品个数上限（超出折叠为「等 N 种商品」，避免摘要超列长）</summary>
    private const int MaxSummaryProductCount = 3;

    private readonly IStockTakeRepository _stockTakeRepository;
    private readonly IProductRepository _productRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IBatchRepository _batchRepository;
    private readonly ISystemClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增盘点单用例处理器
    /// </summary>
    public CreateStockTakeRequestHandler(
        IStockTakeRepository stockTakeRepository,
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository,
        IInventoryRepository inventoryRepository,
        IStockMovementRepository stockMovementRepository,
        IBatchRepository batchRepository,
        ISystemClock clock,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _stockTakeRepository = stockTakeRepository;
        _productRepository = productRepository;
        _warehouseRepository = warehouseRepository;
        _inventoryRepository = inventoryRepository;
        _stockMovementRepository = stockMovementRepository;
        _batchRepository = batchRepository;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理新增盘点 / 期初建账单请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<StockTakeDetailDto> HandleAsync(CreateStockTakeRequest request, CancellationToken cancellationToken = default)
    {
        // 双保险：明细非空（Validator 已拦格式层）
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new BusinessException(ErrorCode.OrderItemsEmpty, "单据明细不能为空");
        }

        // 查库约束：商品逐行存在 / 启用；同时收集编码 / 名称 / 单位快照
        var products = new Dictionary<Guid, Product>(request.Items.Count);
        foreach (var line in request.Items)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                product = await _productRepository.GetByIdAsync(line.ProductId, cancellationToken);
                if (product is null)
                {
                    throw new BusinessException(ErrorCode.NotFound, "商品不存在");
                }

                if (product.Status == ProductStatus.Disabled)
                {
                    throw new BusinessException(ErrorCode.ProductDisabled, "商品已停用，不可用于建账 / 盘点");
                }

                products[line.ProductId] = product;
            }
        }

        // 盘点仓解析（038 §3.4 第 1 步）：入参可空 → 默认仓；指定仓不存在 40400、已停用 40123
        var warehouse = await WarehouseResolver.ResolveAsync(_warehouseRepository, request.WarehouseId, cancellationToken);

        // 批次解析（040，事务外快速失败，失败时不写任何数据）：
        // 按批次商品明细必填批次（40127）、不存在 / 不属于该商品 40400、停用 40000、非批次商品传批次 40000；
        // 盘点为账实核对（无出库 / 入库侧），不拦截过期批次
        var resolvedBatches = new List<ResolvedBatch?>(request.Items.Count);
        var allBatchIds = new HashSet<Guid>();
        for (var index = 0; index < request.Items.Count; index++)
        {
            var line = request.Items[index];
            var product = products[line.ProductId];
            var resolved = await BatchLineResolver.ResolveAsync(
                product.IsBatchManaged,
                product.Id,
                line.BatchId,
                null,
                null,
                null,
                _batchRepository,
                outbound: false,
                _clock.Today,
                _currentUser.UserId(),
                cancellationToken);
            resolvedBatches.Add(resolved);
            if (resolved is not null)
            {
                allBatchIds.Add(resolved.BatchId);
            }
        }

        // 查库约束：期初建账只允许「该仓从未发生库存变动」的行（事务外快速失败，失败时不写任何数据）：
        // 非批次商品按商品判定；批次商品按 (商品, 仓, 批次) 判定（批次首次建账允许）
        if (request.Type == StockTakeType.Initial)
        {
            var nonBatchProductIds = new HashSet<Guid>();
            for (var index = 0; index < request.Items.Count; index++)
            {
                if (resolvedBatches[index] is null)
                {
                    nonBatchProductIds.Add(request.Items[index].ProductId);
                }
            }

            if (nonBatchProductIds.Count > 0)
            {
                var productsWithMovements = await _stockMovementRepository.GetProductIdsWithMovementsAsync(
                    nonBatchProductIds.ToList(), warehouse.Id, cancellationToken);
                if (productsWithMovements.Count > 0)
                {
                    throw new BusinessException(ErrorCode.StockInitialNotAllowed, "期初建账仅允许从未发生库存变动的商品");
                }
            }

            if (allBatchIds.Count > 0)
            {
                var batchIdsWithMovements = await _stockMovementRepository.GetBatchIdsWithMovementsAsync(
                    allBatchIds.ToList(), warehouse.Id, cancellationToken);
                if (batchIdsWithMovements.Count > 0)
                {
                    throw new BusinessException(ErrorCode.StockInitialNotAllowed, "期初建账仅允许从未发生库存变动的批次");
                }
            }
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        var movementType = request.Type == StockTakeType.Initial ? StockMovementType.InitialStock : StockMovementType.StockTakeAdjust;

        // 事务：读账面（事务内读，见 design.md §5 决策）+ 重算差异 + 插单明细 + 逐差异行库存设定 + 写流水；
        // 单号冲突（唯一索引）时回滚后重新生成单号重试
        for (var attempt = 1; ; attempt++)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var takeNo = await _stockTakeRepository.GenerateTakeNoAsync(request.TakeDate, cancellationToken);

                // 事务内读**所选仓**账面（该仓无库存行视为 0），重算差异；前后端差异值不信任前端。
                // 040 按批次盘点：批次行按 (商品, 仓, 批次) 读，非批次行按 (商品, 仓) 读（原 038 语义）
                var batchQuantities = new Dictionary<Guid, IReadOnlyDictionary<Guid, int>>();
                IReadOnlyDictionary<Guid, int> nonBatchQuantities = new Dictionary<Guid, int>();
                var nonBatchProductIds = request.Items
                    .Where((line, index) => resolvedBatches[index] is null)
                    .Select(line => line.ProductId)
                    .Distinct()
                    .ToList();
                if (nonBatchProductIds.Count > 0)
                {
                    nonBatchQuantities = await _inventoryRepository.GetQuantitiesAsync(
                        warehouse.Id, nonBatchProductIds, cancellationToken);
                }

                var items = new List<StockTakeItem>(request.Items.Count);
                var diffItemCount = 0;
                for (var index = 0; index < request.Items.Count; index++)
                {
                    var line = request.Items[index];
                    var p = products[line.ProductId];
                    var batch = resolvedBatches[index];
                    int book;
                    if (batch is not null)
                    {
                        if (!batchQuantities.TryGetValue(line.ProductId, out var byBatch))
                        {
                            byBatch = await _inventoryRepository.GetBatchQuantitiesAsync(
                                warehouse.Id, line.ProductId, cancellationToken);
                            batchQuantities[line.ProductId] = byBatch;
                        }

                        book = byBatch.TryGetValue(batch.BatchId, out var bq) ? bq : 0;
                    }
                    else
                    {
                        book = nonBatchQuantities.TryGetValue(line.ProductId, out var q) ? q : 0;
                    }

                    var difference = line.ActualQuantity - book;
                    if (difference != 0)
                    {
                        diffItemCount++;
                    }

                    items.Add(new StockTakeItem
                    {
                        Id = SequentialGuidGenerator.NewSequential(),
                        ProductId = line.ProductId,
                        BatchId = batch?.BatchId,
                        BatchNo = batch?.BatchNo,
                        ProductCode = p.Code,
                        ProductName = p.Name,
                        Unit = p.Unit,
                        BookQuantity = book,
                        ActualQuantity = line.ActualQuantity,
                        Difference = difference,
                        // 期初成本单价（成本基线，Validator 保证必填）；盘点模式为 0 —— 按当时均价处理
                        UnitCost = request.Type == StockTakeType.Initial ? line.UnitCost ?? 0m : 0m,
                    });
                }

                var take = new StockTake
                {
                    Id = Guid.NewGuid(),
                    TakeNo = takeNo,
                    Type = request.Type,
                    WarehouseId = warehouse.Id,
                    WarehouseName = warehouse.Name,
                    TakeDate = request.TakeDate,
                    ItemCount = items.Count,
                    DiffItemCount = diffItemCount,
                    Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = operatorId,
                    UpdatedBy = operatorId,
                };

                // 明细行挂到主表（明细先构建、后生成主表 Id，统一回填外键）
                foreach (var item in items)
                {
                    item.StockTakeId = take.Id;
                }

                await _stockTakeRepository.AddAsync(take, items, cancellationToken);

                // 只有差异行改库存并写流水（Difference == 0 只落明细）
                foreach (var item in items)
                {
                    if (item.Difference == 0)
                    {
                        continue;
                    }

                    // 库存按实盘数量设定（原子，按所选仓；040 起批次行带批次定位）
                    await _inventoryRepository.SetQuantityAsync(
                        item.ProductId, warehouse.Id, item.BatchId, item.ActualQuantity, cancellationToken);

                    // 成本（erp-cost design §0.2）：期初按录入单价加权；盘点按该仓批次行当时移动加权均价（盘盈入 / 盘亏出）
                    var unitCost = request.Type == StockTakeType.Initial
                        ? item.UnitCost
                        : await _inventoryRepository.GetAverageCostAsync(item.ProductId, warehouse.Id, item.BatchId, cancellationToken);
                    var absQuantity = Math.Abs(item.Difference);
                    var totalCost = CostCalculator.TotalCost(absQuantity, unitCost);
                    if (item.Difference > 0)
                    {
                        await _inventoryRepository.ApplyInboundCostAsync(
                            item.ProductId, warehouse.Id, item.BatchId, absQuantity, unitCost, cancellationToken);
                    }
                    else
                    {
                        await _inventoryRepository.ApplyOutboundCostAsync(
                            item.ProductId, warehouse.Id, item.BatchId, totalCost, cancellationToken);
                    }

                    // 流水：与库存设定同事务，变动量 = 差异（带符号），指向盘点单并带变动仓（040 起带批次）
                    await _stockMovementRepository.AppendAsync(new StockMovement
                    {
                        Id = Guid.NewGuid(),
                        ProductId = item.ProductId,
                        BatchId = item.BatchId,
                        WarehouseId = warehouse.Id,
                        MovementType = movementType,
                        Quantity = item.Difference,
                        UnitCost = unitCost,
                        TotalCost = item.Difference > 0 ? totalCost : -totalCost,
                        SourceId = take.Id,
                        SourceNo = takeNo,
                        CreatedAt = now,
                        CreatedBy = operatorId,
                    }, cancellationToken);
                }

                // 业务写成功后、提交前追加操作日志：与业务同事务，异常回滚则不产生日志
                var productNames = items.Select(i => i.ProductName).Distinct(StringComparer.Ordinal).ToList();
                var involvedText = productNames.Count > MaxSummaryProductCount
                    ? $"{AuditSummary.Join(productNames.Take(MaxSummaryProductCount))} 等 {AuditSummary.Count(productNames.Count)} 种商品"
                    : AuditSummary.Join(productNames);
                var changeBuilder = new AuditChangeBuilder()
                    .Add("takeNo", "盘点单号", null, take.TakeNo)
                    .Add("type", "单据类型", null, AuditText.StockTakeType(take.Type))
                    .Add("warehouseName", "盘点仓", null, take.WarehouseName)
                    .Add("takeDate", "盘点日期", null, AuditSummary.Date(take.TakeDate))
                    .Add("itemCount", "明细行数", null, AuditSummary.Count(take.ItemCount))
                    .Add("diffItemCount", "差异行数", null, AuditSummary.Count(take.DiffItemCount))
                    .Add("productNames", "涉及商品", null, AuditSummary.Join(productNames))
                    .Add("remark", "备注", null, take.Remark);
                await _auditLogger.RecordAsync(new AuditEntry
                {
                    Resource = AuditResource.StockTake,
                    Action = AuditAction.Adjust,
                    ResourceId = take.Id,
                    ResourceNo = take.TakeNo,
                    Summary = $"{AuditText.StockTakeType(take.Type)} {take.TakeNo}（{take.WarehouseName}）：{AuditSummary.Count(take.ItemCount)} 行、差异 {AuditSummary.Count(take.DiffItemCount)} 行、涉及 {involvedText}{AuditSummary.BatchItems(items, i => i.ProductName, i => i.BatchNo)}",
                    Changes = changeBuilder.Build(),
                    ChangesTruncated = changeBuilder.Truncated,
                    UtcNow = now,
                }, cancellationToken);

                await _unitOfWork.CommitAsync(cancellationToken);

                var (createdTake, createdItems) = await _stockTakeRepository.GetDetailAsync(take.Id, cancellationToken);
                if (createdTake is null)
                {
                    throw new BusinessException(ErrorCode.NotFound, "盘点单创建后读取失败");
                }

                return StockTakeDtoMapper.ToStockTakeDetailDto(createdTake, createdItems);
            }
            catch (OrderNoConflictException) when (attempt < MaxTakeNoAttempts)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                continue;
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}
