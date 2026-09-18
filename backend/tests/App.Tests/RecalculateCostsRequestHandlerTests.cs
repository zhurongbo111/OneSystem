using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Costs;
using App.Core.Features.Costs.RecalculateCosts;

namespace App.Tests;

/// <summary>
/// 成本重算用例测试（specs/026-erp-cost design.md §6）：
/// 按流水时序推演的正确性、冲销还原、缺价统计、幂等性、并发拒绝（40118）与「不改数量」。
/// </summary>
public class RecalculateCostsRequestHandlerTests
{
    private static readonly DateTimeOffset Base = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static StockMovement Movement(
        Guid productId, StockMovementType type, int quantity, Guid sourceId, DateTimeOffset createdAt)
        => new()
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            MovementType = type,
            Quantity = quantity,
            SourceId = sourceId,
            SourceNo = sourceId.ToString()[..8],
            CreatedAt = createdAt,
        };

    /// <summary>构造「期初 → 入库 → 出库 → 采购作废」的流水链与依赖</summary>
    private static (Guid ProductId, FakeStockMovementRepository Movements, FakeInventoryRepository Inventory,
        RecordingUnitOfWork Uow, CostRecalculationLock Lock, RecalculateCostsRequestHandler Handler) CreateChain()
    {
        var productId = Guid.NewGuid();
        var takeId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var saleId = Guid.NewGuid();

        var movements = new FakeStockMovementRepository();
        movements.InitialUnitCosts[(takeId, productId)] = 10m;
        movements.PurchaseUnitPrices[(receiptId, productId)] = 20m;
        movements.Appended.AddRange(new[]
        {
            Movement(productId, StockMovementType.InitialStock, 10, takeId, Base),
            Movement(productId, StockMovementType.PurchaseInbound, 10, receiptId, Base.AddHours(1)),
            Movement(productId, StockMovementType.SalesOutbound, -5, saleId, Base.AddHours(2)),
            Movement(productId, StockMovementType.PurchaseVoid, -10, receiptId, Base.AddHours(3)),
        });

        var inventory = new FakeInventoryRepository();
        inventory.Seed(productId, 5);

        var uow = new RecordingUnitOfWork();
        var recalculationLock = new CostRecalculationLock();
        var handler = new RecalculateCostsRequestHandler(movements, inventory, uow, recalculationLock);

        return (productId, movements, inventory, uow, recalculationLock, handler);
    }

    [Fact]
    public async Task 重算_应按流水时序推演成本并写回流水成本列与库存成本列()
    {
        var (productId, movements, inventory, _, _, handler) = CreateChain();

        var result = await handler.HandleAsync(new RecalculateCostsRequest());

        // 期初 10×10 → 入库 10×20（均价 15）→ 出库 5（按均价结转 75）→ 作废 −10（按原入库单价 20 回冲）
        Assert.Equal(10m, movements.Appended[0].UnitCost);
        Assert.Equal(100m, movements.Appended[0].TotalCost);
        Assert.Equal(20m, movements.Appended[1].UnitCost);
        Assert.Equal(200m, movements.Appended[1].TotalCost);
        Assert.Equal(15m, movements.Appended[2].UnitCost);
        Assert.Equal(-75m, movements.Appended[2].TotalCost);
        Assert.Equal(20m, movements.Appended[3].UnitCost);
        Assert.Equal(-200m, movements.Appended[3].TotalCost);

        // 结存：数量 5、金额 25（100 + 200 − 75 − 200）、均价 5
        Assert.Equal(25m, inventory.CostAmounts[productId]);
        Assert.Equal(5m, inventory.AverageCosts[productId]);
        Assert.Equal(5, inventory.GetQuantity(productId)); // 重算不改数量

        Assert.Equal(4, result.MovementCount);
        Assert.Equal(0, result.MissingCostCount);
        Assert.Equal(1, result.ProductCount);
    }

    [Fact]
    public async Task 重算_连续两次执行结果应完全一致()
    {
        var (productId, movements, inventory, _, _, handler) = CreateChain();

        var first = await handler.HandleAsync(new RecalculateCostsRequest());
        var firstUpdates = movements.CostUpdates.ToList();
        var firstAmount = inventory.CostAmounts[productId];
        var firstAverage = inventory.AverageCosts[productId];

        var second = await handler.HandleAsync(new RecalculateCostsRequest());

        // 幂等：第二轮写回的（流水 id, 单价, 金额）序列与第一轮完全相同
        Assert.Equal(firstUpdates, movements.CostUpdates.Skip(firstUpdates.Count).ToList());
        Assert.Equal(firstAmount, inventory.CostAmounts[productId]);
        Assert.Equal(firstAverage, inventory.AverageCosts[productId]);
        Assert.Equal(first.MovementCount, second.MovementCount);
        Assert.Equal(first.MissingCostCount, second.MissingCostCount);
        Assert.Equal(first.ProductCount, second.ProductCount);
    }

    [Fact]
    public async Task 重算_缺价应按0计入并统计缺价条数()
    {
        var productId = Guid.NewGuid();
        var movements = new FakeStockMovementRepository();
        // 期初流水未预置成本单价 → 单价无法推算
        movements.Appended.Add(Movement(productId, StockMovementType.InitialStock, 10, Guid.NewGuid(), Base));

        var inventory = new FakeInventoryRepository();
        inventory.Seed(productId, 10);
        var handler = new RecalculateCostsRequestHandler(
            movements, inventory, new RecordingUnitOfWork(), new CostRecalculationLock());

        var result = await handler.HandleAsync(new RecalculateCostsRequest());

        Assert.Equal(1, result.MissingCostCount);
        Assert.Equal(0m, movements.Appended[0].UnitCost);
        Assert.Equal(0m, movements.Appended[0].TotalCost);
        Assert.Equal(0m, inventory.CostAmounts[productId]);
    }

    [Fact]
    public async Task 重算_进行中再次触发应返回40118并发拒绝()
    {
        var recalculationLock = new CostRecalculationLock();
        Assert.True(recalculationLock.TryEnter()); // 占住锁，模拟重算进行中

        var handler = new RecalculateCostsRequestHandler(
            new FakeStockMovementRepository(), new FakeInventoryRepository(), new RecordingUnitOfWork(), recalculationLock);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new RecalculateCostsRequest()));
        Assert.Equal(ErrorCode.CostRecalculationRunning, exception.Code);
    }

    [Fact]
    public async Task 重算_应按期间过滤写回范围但推演仍从最早流水开始()
    {
        var productId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();

        var movements = new FakeStockMovementRepository();
        movements.PurchaseUnitPrices[(receiptId, productId)] = 20m;
        movements.Appended.AddRange(new[]
        {
            // 期间之前的入库（只用于推演结存，不在写回范围内）
            Movement(productId, StockMovementType.PurchaseInbound, 10, Guid.NewGuid(), Base),
            // 期间内的入库（写回）
            Movement(productId, StockMovementType.PurchaseInbound, 10, receiptId, Base.AddDays(5)),
        });

        var inventory = new FakeInventoryRepository();
        inventory.Seed(productId, 20);
        var handler = new RecalculateCostsRequestHandler(
            movements, inventory, new RecordingUnitOfWork(), new CostRecalculationLock());

        var result = await handler.HandleAsync(new RecalculateCostsRequest
        {
            Start = Base.AddDays(1),
            End = Base.AddDays(10),
        });

        // 只写回期间内的 1 条；结存按全部流水推演（数量 20、金额 200）
        Assert.Equal(1, result.MovementCount);
        Assert.Equal(movements.Appended[1].Id, Assert.Single(movements.CostUpdates).Id);
        Assert.Equal(200m, inventory.CostAmounts[productId]);
    }
}
