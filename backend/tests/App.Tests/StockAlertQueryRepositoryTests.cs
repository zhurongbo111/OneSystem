using App.Core.Entities;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 库存预警只读查询仓储单测（041 §6「判定口径」）：
/// 低库存（阈值 0 / 库存 = 阈值 / 按商品 × 仓汇总 / 商品停用）、
/// 近效期（今天 + 30 命中、+31 不命中、已过期不重复算近效期、库存 0 不告警）、
/// 过期（已过期且仍有库存才告警）。判定口径唯一来源见 041 design.md §0。
/// </summary>
public class StockAlertQueryRepositoryTests
{
    private static readonly DateOnly Today = new(2026, 10, 6);

    private static DateTimeOffset UtcDate(int year, int month, int day)
        => new(new DateTime(year, month, day), TimeSpan.Zero);

    private static void SeedInventory(
        AppDbContext dbContext,
        Product product,
        Warehouse warehouse,
        int quantity,
        int safetyStock = 0,
        Batch? batch = null)
    {
        dbContext.Inventory.Add(new Inventory
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            BatchId = batch?.Id,
            Quantity = quantity,
            SafetyStock = safetyStock,
            CostAmount = 0m,
            AverageCost = 0m,
            UpdatedAt = UtcDate(2026, 10, 1),
        });
    }

    private static Batch NewBatch(Product product, string batchNo, DateTimeOffset? expiryDate)
        => new()
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            BatchNo = batchNo,
            ExpiryDate = expiryDate,
            Status = PartnerStatus.Enabled,
            CreatedAt = UtcDate(2026, 9, 1),
            UpdatedAt = UtcDate(2026, 9, 1),
        };

    [Fact]
    public async Task 低库存_阈值大于零且库存低于阈值_应命中并汇总多行()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var warehouse = TestSupport.NewWarehouse();
        var product = TestSupport.NewProduct("SKU-L1", "低库存商品");
        dbContext.Warehouses.Add(warehouse);
        dbContext.Products.Add(product);
        var batch = NewBatch(product, "B1", null);
        dbContext.Batches.Add(batch);
        // 同一「商品 × 仓」两行（非批次行 + 批次行）合计 10 < 阈值 100
        SeedInventory(dbContext, product, warehouse, 4, safetyStock: 100);
        SeedInventory(dbContext, product, warehouse, 6, safetyStock: 100, batch: batch);
        await dbContext.SaveChangesAsync();

        var signals = await new StockAlertQueryRepository(dbContext).GetLowStockSignalsAsync(500);

        var signal = Assert.Single(signals);
        Assert.Equal(product.Id, signal.ProductId);
        Assert.Equal(warehouse.Id, signal.WarehouseId);
        Assert.Equal("主仓", signal.WarehouseName);
        Assert.Equal(10, signal.Quantity);
        Assert.Equal(100, signal.SafetyStock);
    }

    [Fact]
    public async Task 低库存_阈值为零或库存等于阈值_均不命中()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var warehouse = TestSupport.NewWarehouse();
        var zeroThreshold = TestSupport.NewProduct("SKU-L2", "不提醒商品");
        var equalThreshold = TestSupport.NewProduct("SKU-L3", "刚好达标商品");
        dbContext.Warehouses.Add(warehouse);
        dbContext.Products.AddRange(zeroThreshold, equalThreshold);
        SeedInventory(dbContext, zeroThreshold, warehouse, 0, safetyStock: 0);
        SeedInventory(dbContext, equalThreshold, warehouse, 100, safetyStock: 100);
        await dbContext.SaveChangesAsync();

        var signals = await new StockAlertQueryRepository(dbContext).GetLowStockSignalsAsync(500);

        Assert.Empty(signals);
    }

    [Fact]
    public async Task 低库存_商品已停用_不命中()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var warehouse = TestSupport.NewWarehouse();
        var product = TestSupport.NewProduct("SKU-L4", "停用商品", status: ProductStatus.Disabled);
        dbContext.Warehouses.Add(warehouse);
        dbContext.Products.Add(product);
        SeedInventory(dbContext, product, warehouse, 1, safetyStock: 100);
        await dbContext.SaveChangesAsync();

        var signals = await new StockAlertQueryRepository(dbContext).GetLowStockSignalsAsync(500);

        Assert.Empty(signals);
    }

    [Fact]
    public async Task 近效期_命中窗口边界_三十天内命中_三十一天不命中_已过期不算近效期()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var warehouse = TestSupport.NewWarehouse();
        var hitProduct = TestSupport.NewProduct("SKU-E1", "窗口内商品");
        var soonProduct = TestSupport.NewProduct("SKU-E2", "窗口外商品");
        var expiredProduct = TestSupport.NewProduct("SKU-E3", "已过期商品");
        dbContext.Warehouses.Add(warehouse);
        dbContext.Products.AddRange(hitProduct, soonProduct, expiredProduct);

        var hitBatch = NewBatch(hitProduct, "B-HIT", UtcDate(2026, 11, 5));      // 今天 + 30 → 命中
        var soonBatch = NewBatch(soonProduct, "B-SOON", UtcDate(2026, 11, 6));   // 今天 + 31 → 不命中
        var expiredBatch = NewBatch(expiredProduct, "B-EXP", UtcDate(2026, 10, 5)); // 已过期 → 不算近效期
        dbContext.Batches.AddRange(hitBatch, soonBatch, expiredBatch);
        SeedInventory(dbContext, hitProduct, warehouse, 5, batch: hitBatch);
        SeedInventory(dbContext, soonProduct, warehouse, 5, batch: soonBatch);
        SeedInventory(dbContext, expiredProduct, warehouse, 5, batch: expiredBatch);
        await dbContext.SaveChangesAsync();

        var signals = await new StockAlertQueryRepository(dbContext)
            .GetExpiringBatchSignalsAsync(Today, BatchFieldConstraints.NearExpiryDays, 500);

        var signal = Assert.Single(signals);
        Assert.Equal(hitProduct.Id, signal.ProductId);
        Assert.Equal("B-HIT", signal.BatchNo);
        Assert.Equal(5, signal.Quantity);
    }

    [Fact]
    public async Task 过期_已过期且仍有库存才命中_库存为零不告警_未过期不告警()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var warehouse = TestSupport.NewWarehouse();
        var expiredProduct = TestSupport.NewProduct("SKU-X1", "过期有库存");
        var emptyProduct = TestSupport.NewProduct("SKU-X2", "过期无库存");
        var futureProduct = TestSupport.NewProduct("SKU-X3", "未过期商品");
        dbContext.Warehouses.Add(warehouse);
        dbContext.Products.AddRange(expiredProduct, emptyProduct, futureProduct);

        var expiredBatch = NewBatch(expiredProduct, "B-EXP", UtcDate(2026, 10, 5));
        var emptyBatch = NewBatch(emptyProduct, "B-EMPTY", UtcDate(2026, 10, 1));
        var futureBatch = NewBatch(futureProduct, "B-FUTURE", UtcDate(2026, 12, 1));
        dbContext.Batches.AddRange(expiredBatch, emptyBatch, futureBatch);
        SeedInventory(dbContext, expiredProduct, warehouse, 3, batch: expiredBatch);
        SeedInventory(dbContext, emptyProduct, warehouse, 0, batch: emptyBatch);
        SeedInventory(dbContext, futureProduct, warehouse, 3, batch: futureBatch);
        await dbContext.SaveChangesAsync();

        var signals = await new StockAlertQueryRepository(dbContext).GetExpiredBatchSignalsAsync(Today, 500);

        var signal = Assert.Single(signals);
        Assert.Equal(expiredProduct.Id, signal.ProductId);
        Assert.Equal("B-EXP", signal.BatchNo);
        Assert.Equal(3, signal.Quantity);
    }
}
