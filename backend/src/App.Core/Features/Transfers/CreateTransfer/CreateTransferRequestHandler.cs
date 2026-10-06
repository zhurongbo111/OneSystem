using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Batches;
using App.Core.Features.Warehouses;

namespace App.Core.Features.Transfers.CreateTransfer;

/// <summary>
/// 新增调拨单用例（一步式：保存即生效）：
/// 双仓校验（存在 / 启用 / 两仓不同）→ 商品逐行校验（存在 / 启用）→ 写编码 / 名称 / 单位快照
/// → 统计 ItemCount / TotalQuantity → 同一事务：先全部转出（TryDecrement + 成本 + 流水），
///   再全部转入（Increment + 成本 + 流水，同一单价保证组织级成本净变化为 0），最后 AddAsync 落单；
///   单号冲突（唯一索引）时回滚后重新生成单号重试最多 3 次。
/// 调拨不写凭证、不涉结算、不落业务金额（见 design.md §0）。
/// </summary>
public sealed class CreateTransferRequestHandler : IRequestHandler<CreateTransferRequest, TransferDetailDto>
{
    /// <summary>单号冲突重试上限（含首次）</summary>
    private const int MaxTransferNoAttempts = 3;

    private readonly ITransferRepository _transferRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IBatchRepository _batchRepository;
    private readonly ISystemClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增调拨单用例处理器
    /// </summary>
    public CreateTransferRequestHandler(
        ITransferRepository transferRepository,
        IWarehouseRepository warehouseRepository,
        IProductRepository productRepository,
        IInventoryRepository inventoryRepository,
        IStockMovementRepository stockMovementRepository,
        IBatchRepository batchRepository,
        ISystemClock clock,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _transferRepository = transferRepository;
        _warehouseRepository = warehouseRepository;
        _productRepository = productRepository;
        _inventoryRepository = inventoryRepository;
        _stockMovementRepository = stockMovementRepository;
        _batchRepository = batchRepository;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理新增调拨单请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<TransferDetailDto> HandleAsync(CreateTransferRequest request, CancellationToken cancellationToken = default)
    {
        // 双保险：明细非空（Validator 已拦格式层）
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new BusinessException(ErrorCode.OrderItemsEmpty, "单据明细不能为空");
        }

        // 两仓相同 → 40126（转出与转入必须不同；放 Handler 统一错误码来源，避免 Validator 与 Handler 两处分叉）
        if (request.FromWarehouseId == request.ToWarehouseId)
        {
            throw new BusinessException(ErrorCode.TransferSameWarehouse, "转出仓与转入仓不能相同");
        }

        // 双仓校验：不存在 40400、停用 40123（038 WarehouseResolver 统一判定）
        var fromWarehouse = await WarehouseResolver.ResolveAsync(_warehouseRepository, request.FromWarehouseId, cancellationToken);
        var toWarehouse = await WarehouseResolver.ResolveAsync(_warehouseRepository, request.ToWarehouseId, cancellationToken);

        // 逐行取商品：不存在 40400、停用 40107；同时收集编码 / 名称 / 单位快照
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
                    throw new BusinessException(ErrorCode.ProductDisabled, "商品已停用，不可用于开单");
                }

                products[line.ProductId] = product;
            }
        }

        var itemCount = request.Items.Count;
        var totalQuantity = request.Items.Sum(i => i.Quantity);
        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        var transferId = Guid.NewGuid();

        // 事务：先全部转出（含成本与流水），再全部转入（同一单价），最后落单；
        // 单号冲突（唯一索引）时回滚后重新生成单号重试——上一轮已扣 / 已加库存随回滚恢复，整轮重来安全。
        for (var attempt = 1; ; attempt++)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var transferNo = await _transferRepository.GenerateTransferNoAsync(request.TransferDate, cancellationToken);

                var transfer = new Transfer
                {
                    Id = transferId,
                    TransferNo = transferNo,
                    FromWarehouseId = fromWarehouse.Id,
                    FromWarehouseName = fromWarehouse.Name,
                    ToWarehouseId = toWarehouse.Id,
                    ToWarehouseName = toWarehouse.Name,
                    TransferDate = request.TransferDate,
                    ItemCount = itemCount,
                    TotalQuantity = totalQuantity,
                    Status = OrderStatus.Normal,
                    Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = operatorId,
                    UpdatedBy = operatorId,
                };

                // 批次解析（040 §3.4，事务内执行）：按批次商品必填、停用 40000、批次不属于该商品 40400；
                // 调拨为仓间位移（无出库/入库侧），不拦截过期批次（outbound = false，040 §0 过期只拦出库类）
                var resolvedBatches = new List<ResolvedBatch?>(request.Items.Count);
                foreach (var line in request.Items)
                {
                    var p0 = products[line.ProductId];
                    resolvedBatches.Add(await BatchLineResolver.ResolveAsync(
                        p0.IsBatchManaged,
                        p0.Id,
                        line.BatchId,
                        null,
                        null,
                        null,
                        _batchRepository,
                        outbound: false,
                        _clock.Today,
                        operatorId,
                        cancellationToken));
                }

                // 明细行按请求顺序生成顺序 Guid（SequentialGuidGenerator），保证持久化顺序与请求顺序一致
                var items = new List<TransferItem>(request.Items.Count);
                for (var index = 0; index < request.Items.Count; index++)
                {
                    var line = request.Items[index];
                    var p = products[line.ProductId];
                    var batch = resolvedBatches[index];
                    items.Add(new TransferItem
                    {
                        Id = SequentialGuidGenerator.NewSequential(),
                        TransferId = transfer.Id,
                        ProductId = line.ProductId,
                        ProductCode = p.Code,
                        ProductName = p.Name,
                        Unit = p.Unit,
                        Quantity = line.Quantity,
                        BatchId = batch?.BatchId,
                        BatchNo = batch?.BatchNo,
                    });
                }

                // 成本：转出时按行批次行的均价（026 §0.1 的批次行版本；040 §0 调拨单价仍取商品均价口径，按行隔离防同商品多批次串价），
                // 转入用同一单价保证组织级成本净变化为 0
                var transferOutUnitCosts = new List<decimal>(request.Items.Count);

                // 逐行全部转出：avgCost → TryDecrement → ApplyOutboundCost → AppendAsync(TransferOut)
                foreach (var item in items)
                {
                    var avgCost = await _inventoryRepository.GetAverageCostAsync(
                        item.ProductId, fromWarehouse.Id, item.BatchId, cancellationToken);
                    transferOutUnitCosts.Add(avgCost);

                    var ok = await _inventoryRepository.TryDecrementAsync(
                        item.ProductId, fromWarehouse.Id, item.BatchId, item.Quantity, cancellationToken);
                    if (!ok)
                    {
                        var current = await _inventoryRepository.GetQuantityAsync(
                            item.ProductId, fromWarehouse.Id, item.BatchId, cancellationToken);
                        var batchNoText = item.BatchNo is null ? string.Empty : $"/批次 {item.BatchNo}";
                        throw new BusinessException(
                            ErrorCode.InsufficientStock,
                            $"库存不足：{fromWarehouse.Name} 商品 {item.ProductName}{batchNoText}（当前 {current}，需要 {item.Quantity}）");
                    }

                    var totalCost = CostCalculator.TotalCost(item.Quantity, avgCost);
                    await _inventoryRepository.ApplyOutboundCostAsync(
                        item.ProductId, fromWarehouse.Id, item.BatchId, totalCost, cancellationToken);

                    await _stockMovementRepository.AppendAsync(new StockMovement
                    {
                        Id = Guid.NewGuid(),
                        ProductId = item.ProductId,
                        BatchId = item.BatchId,
                        WarehouseId = fromWarehouse.Id,
                        MovementType = StockMovementType.TransferOut,
                        Quantity = -item.Quantity,
                        UnitCost = avgCost,
                        TotalCost = -totalCost,
                        SourceId = transfer.Id,
                        SourceNo = transferNo,
                        CreatedAt = now,
                        CreatedBy = operatorId,
                    }, cancellationToken);
                }

                // 逐行全部转入：Increment → ApplyInboundCost(同一 avgCost) → AppendAsync(TransferIn)
                for (var index = 0; index < items.Count; index++)
                {
                    var item = items[index];
                    var avgCost = transferOutUnitCosts[index];
                    var totalCost = CostCalculator.TotalCost(item.Quantity, avgCost);

                    await _inventoryRepository.IncrementAsync(
                        item.ProductId, toWarehouse.Id, item.BatchId, item.Quantity, cancellationToken);

                    await _inventoryRepository.ApplyInboundCostAsync(
                        item.ProductId, toWarehouse.Id, item.BatchId, item.Quantity, avgCost, cancellationToken);

                    await _stockMovementRepository.AppendAsync(new StockMovement
                    {
                        Id = Guid.NewGuid(),
                        ProductId = item.ProductId,
                        BatchId = item.BatchId,
                        WarehouseId = toWarehouse.Id,
                        MovementType = StockMovementType.TransferIn,
                        Quantity = item.Quantity,
                        UnitCost = avgCost,
                        TotalCost = totalCost,
                        SourceId = transfer.Id,
                        SourceNo = transferNo,
                        CreatedAt = now,
                        CreatedBy = operatorId,
                    }, cancellationToken);
                }

                await _transferRepository.AddAsync(transfer, items, cancellationToken);

                // 业务写成功后、提交前追加操作日志：与业务同事务，异常回滚则不产生日志
                var createdTransferChangeBuilder = new AuditChangeBuilder()
                    .Add("transferNo", "调拨单号", null, transfer.TransferNo)
                    .Add("fromWarehouseName", "转出仓", null, transfer.FromWarehouseName)
                    .Add("toWarehouseName", "转入仓", null, transfer.ToWarehouseName)
                    .Add("transferDate", "调拨日期", null, AuditSummary.Date(transfer.TransferDate))
                    .Add("remark", "备注", null, transfer.Remark);
                await _auditLogger.RecordAsync(new AuditEntry
                {
                    Resource = AuditResource.Transfer,
                    Action = AuditAction.Create,
                    ResourceId = transfer.Id,
                    ResourceNo = transfer.TransferNo,
                    Summary = $"创建调拨单 {transfer.TransferNo}（转出仓：{transfer.FromWarehouseName}、转入仓：{transfer.ToWarehouseName}、{AuditSummary.Count(items.Count)} 行、{AuditSummary.Quantity(totalQuantity)}）{AuditSummary.BatchItems(items, i => i.ProductName, i => i.BatchNo)}",
                    Changes = createdTransferChangeBuilder.Build(),
                    ChangesTruncated = createdTransferChangeBuilder.Truncated,
                    UtcNow = now,
                }, cancellationToken);

                await _unitOfWork.CommitAsync(cancellationToken);

                var (createdTransfer, createdItems) = await _transferRepository.GetDetailAsync(transfer.Id, cancellationToken: cancellationToken);
                if (createdTransfer is null)
                {
                    throw new BusinessException(ErrorCode.NotFound, "调拨单创建后读取失败");
                }

                return TransfersDtoMapper.ToTransferDetailDto(createdTransfer, createdItems);
            }
            catch (OrderNoConflictException) when (attempt < MaxTransferNoAttempts)
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
