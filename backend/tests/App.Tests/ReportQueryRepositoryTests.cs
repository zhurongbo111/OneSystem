using App.Core.Entities;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// 报表口径与对账一致性测试（真实 <see cref="ReportQueryRepository"/> + InMemory 只读场景，无事务 / 行锁依赖）：
/// 期末 = 期初 + 期间入 − 期间出、期末与库存台账一致、盘点调整按符号双向拆分、期间左闭右开、
/// 期初不随筛选漂移、余额表低库存口径与库存查询页同源、汇总净额与作废过滤。
/// </summary>
public class ReportQueryRepositoryTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset End = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Guid CategoryAId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid CategoryBId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static Category NewCategory(Guid id, string name) => new() { Id = id, Name = name, CreatedAt = Start };

    private static Product NewProduct(string code, Guid categoryId, int safetyStock = 0)
        => new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = $"商品{code}",
            CategoryId = categoryId,
            Unit = "个",
            SafetyStock = safetyStock,
            Status = ProductStatus.Enabled,
            CreatedAt = Start,
            UpdatedAt = Start,
        };

    private static StockMovement NewMovement(Guid productId, StockMovementType type, int quantity, DateTimeOffset createdAt)
        => new()
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            MovementType = type,
            Quantity = quantity,
            CreatedAt = createdAt,
        };

    /// <summary>
    /// 库存行（038：一行 = 商品 × 仓；仓级安全库存为低库存判定的唯一来源，故与商品阈值同值传入）
    /// </summary>
    private static Inventory NewInventory(Guid productId, int quantity, int safetyStock = 0, Guid? warehouseId = null)
        => new()
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            WarehouseId = warehouseId ?? TestWarehouse.DefaultId,
            Quantity = quantity,
            SafetyStock = safetyStock,
            UpdatedAt = End,
        };

    private static PurchaseReceipt NewReceipt(Guid partnerId, DateTimeOffset orderDate, decimal totalAmount, OrderStatus status = OrderStatus.Normal)
    {
        var id = Guid.NewGuid();
        return new PurchaseReceipt
        {
            Id = id,
            ReceiptNo = $"GR{orderDate:yyyyMMdd}{id.ToString("N")[..4]}",
            PartnerId = partnerId,
            PartnerName = "供应商一",
            OrderDate = orderDate,
            TotalAmount = totalAmount,
            Status = status,
            CreatedAt = orderDate,
            UpdatedAt = orderDate,
        };
    }

    private static PurchaseReceiptItem NewReceiptItem(Guid receiptId, Guid productId, int quantity, decimal unitPrice, string productName = "商品一")
        => new()
        {
            Id = Guid.NewGuid(),
            ReceiptId = receiptId,
            ProductId = productId,
            ProductName = productName,
            Unit = "个",
            Quantity = quantity,
            UnitPrice = unitPrice,
            Subtotal = quantity * unitPrice,
        };

    private static PurchaseReturn NewReturn(Guid partnerId, DateTimeOffset returnDate, decimal totalAmount, OrderStatus status = OrderStatus.Normal)
    {
        var id = Guid.NewGuid();
        return new PurchaseReturn
        {
            Id = id,
            ReturnNo = $"PR{returnDate:yyyyMMdd}{id.ToString("N")[..4]}",
            PartnerId = partnerId,
            PartnerName = "供应商一",
            ReturnDate = returnDate,
            TotalAmount = totalAmount,
            Status = status,
            CreatedAt = returnDate,
            UpdatedAt = returnDate,
        };
    }

    private static PurchaseReturnItem NewReturnItem(Guid returnId, Guid productId, int quantity, decimal unitPrice, string productName = "商品一")
        => new()
        {
            Id = Guid.NewGuid(),
            ReturnId = returnId,
            ProductId = productId,
            ProductName = productName,
            Unit = "个",
            Quantity = quantity,
            UnitPrice = unitPrice,
            Subtotal = quantity * unitPrice,
        };

    // ============================== 进销存报表：口径与对账 ==============================

    [Fact]
    public async Task 进销存报表_期末应等于库存台账且恒等式成立()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var product = NewProduct("sku-1", CategoryAId);
        dbContext.Categories.Add(NewCategory(CategoryAId, "分类一"));
        dbContext.Products.Add(product);
        dbContext.Inventory.Add(NewInventory(product.Id, 85));

        // 期初建账 100 → 采购入库 20 → 销售出库 30 → 盘点调整 −5，台账 85
        dbContext.StockMovements.AddRange(
            NewMovement(product.Id, StockMovementType.InitialStock, 100, Start.AddDays(-10)),
            NewMovement(product.Id, StockMovementType.PurchaseInbound, 20, Start.AddDays(1)),
            NewMovement(product.Id, StockMovementType.SalesOutbound, -30, Start.AddDays(2)),
            NewMovement(product.Id, StockMovementType.StockTakeAdjust, -5, Start.AddDays(3)));
        await dbContext.SaveChangesAsync();

        var repository = new ReportQueryRepository(dbContext);
        var (items, total, summary) = await repository.GetInventoryFlowAsync(Start, End, null, null, false, null, 1, 20);

        var item = Assert.Single(items);
        Assert.Equal(1, total);
        Assert.Equal(100, item.OpeningQuantity);
        Assert.Equal(20, item.InboundQuantity);
        // 盘点 −5 按符号计入期间出（不是整条计入一侧）
        Assert.Equal(35, item.OutboundQuantity);
        Assert.Equal(85, item.ClosingQuantity);
        Assert.Equal(item.OpeningQuantity + item.InboundQuantity - item.OutboundQuantity, item.ClosingQuantity);

        var ledger = await dbContext.Inventory
            .Where(i => i.ProductId == product.Id)
            .Select(i => i.Quantity)
            .SingleAsync();
        Assert.Equal(ledger, item.ClosingQuantity);
        Assert.Equal(ledger, summary.ClosingQuantity);
    }

    [Fact]
    public async Task 进销存报表_起点前流水应全部计入期初()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var product = NewProduct("sku-1", CategoryAId);
        dbContext.Categories.Add(NewCategory(CategoryAId, "分类一"));
        dbContext.Products.Add(product);
        dbContext.StockMovements.AddRange(
            NewMovement(product.Id, StockMovementType.InitialStock, 100, Start.AddDays(-20)),
            NewMovement(product.Id, StockMovementType.SalesOutbound, -30, Start.AddDays(-10)),
            NewMovement(product.Id, StockMovementType.PurchaseInbound, 20, Start.AddDays(1)));
        await dbContext.SaveChangesAsync();

        var repository = new ReportQueryRepository(dbContext);
        var (items, _, _) = await repository.GetInventoryFlowAsync(Start, End, null, null, false, null, 1, 20);

        var item = Assert.Single(items);
        Assert.Equal(70, item.OpeningQuantity);
        Assert.Equal(20, item.InboundQuantity);
        Assert.Equal(0, item.OutboundQuantity);
        Assert.Equal(90, item.ClosingQuantity);
    }

    [Fact]
    public async Task 进销存报表_盘点调整应按符号分别计入入与出()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var product = NewProduct("sku-1", CategoryAId);
        dbContext.Categories.Add(NewCategory(CategoryAId, "分类一"));
        dbContext.Products.Add(product);
        dbContext.StockMovements.AddRange(
            NewMovement(product.Id, StockMovementType.StockTakeAdjust, 5, Start.AddDays(1)),
            NewMovement(product.Id, StockMovementType.StockTakeAdjust, -3, Start.AddDays(2)));
        await dbContext.SaveChangesAsync();

        var repository = new ReportQueryRepository(dbContext);
        var (items, _, _) = await repository.GetInventoryFlowAsync(Start, End, null, null, false, null, 1, 20);

        var item = Assert.Single(items);
        Assert.Equal(5, item.InboundQuantity);
        Assert.Equal(3, item.OutboundQuantity);
        Assert.Equal(2, item.ClosingQuantity);
    }

    [Fact]
    public async Task 进销存报表_期间为左闭右开_结束时刻流水不计入期间()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var product = NewProduct("sku-1", CategoryAId);
        dbContext.Categories.Add(NewCategory(CategoryAId, "分类一"));
        dbContext.Products.Add(product);
        dbContext.StockMovements.AddRange(
            NewMovement(product.Id, StockMovementType.PurchaseInbound, 20, Start),
            NewMovement(product.Id, StockMovementType.PurchaseInbound, 7, End));
        await dbContext.SaveChangesAsync();

        var repository = new ReportQueryRepository(dbContext);
        var (items, _, _) = await repository.GetInventoryFlowAsync(Start, End, null, null, false, null, 1, 20);

        var item = Assert.Single(items);
        // 起点（含）计入期间，终点（不含）留给下一期间
        Assert.Equal(20, item.InboundQuantity);
        Assert.Equal(0, item.OpeningQuantity);
    }

    [Fact]
    public async Task 进销存报表_只看有变动时应排除期间无变动商品且合计收敛()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var changed = NewProduct("sku-1", CategoryAId);
        var unchanged = NewProduct("sku-2", CategoryAId);
        dbContext.Categories.Add(NewCategory(CategoryAId, "分类一"));
        dbContext.Products.AddRange(changed, unchanged);
        dbContext.StockMovements.AddRange(
            NewMovement(changed.Id, StockMovementType.PurchaseInbound, 20, Start.AddDays(1)),
            // sku-2 仅有期初余额，期间无变动
            NewMovement(unchanged.Id, StockMovementType.InitialStock, 50, Start.AddDays(-5)));
        await dbContext.SaveChangesAsync();

        var repository = new ReportQueryRepository(dbContext);

        var (allItems, allTotal, allSummary) = await repository.GetInventoryFlowAsync(Start, End, null, null, false, null, 1, 20);
        Assert.Equal(2, allTotal);
        Assert.Equal(50, allSummary.OpeningQuantity);

        var (changedItems, changedTotal, changedSummary) = await repository.GetInventoryFlowAsync(Start, End, null, null, true, null, 1, 20);
        Assert.Equal(1, changedTotal);
        Assert.Equal("sku-1", Assert.Single(changedItems).Code);
        Assert.Equal(0, changedSummary.OpeningQuantity);
        Assert.Equal(20, changedSummary.InboundQuantity);
    }

    [Fact]
    public async Task 进销存报表_按分类筛选时期初仍为该商品起点前累计_不漂移()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var productA = NewProduct("sku-a", CategoryAId);
        var productB = NewProduct("sku-b", CategoryBId);
        dbContext.Categories.AddRange(NewCategory(CategoryAId, "分类一"), NewCategory(CategoryBId, "分类二"));
        dbContext.Products.AddRange(productA, productB);
        dbContext.StockMovements.AddRange(
            NewMovement(productA.Id, StockMovementType.InitialStock, 100, Start.AddDays(-30)),
            NewMovement(productB.Id, StockMovementType.InitialStock, 50, Start.AddDays(-30)));
        await dbContext.SaveChangesAsync();

        var repository = new ReportQueryRepository(dbContext);
        var (items, total, summary) = await repository.GetInventoryFlowAsync(Start, End, null, CategoryAId, false, null, 1, 20);

        Assert.Equal(1, total);
        Assert.Equal(100, Assert.Single(items).OpeningQuantity);
        Assert.Equal(100, summary.OpeningQuantity);
    }

    // ============================== 库存余额表：分类聚合与低库存口径 ==============================

    [Fact]
    public async Task 库存余额表_应按分类聚合并与库存查询页低库存口径同源()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var below = NewProduct("sku-1", CategoryAId, safetyStock: 10);
        var zeroNoAlert = NewProduct("sku-2", CategoryAId, safetyStock: 0);
        var normal = NewProduct("sku-3", CategoryAId, safetyStock: 10);
        dbContext.Categories.Add(NewCategory(CategoryAId, "分类一"));
        dbContext.Products.AddRange(below, zeroNoAlert, normal);
        dbContext.Inventory.AddRange(
            NewInventory(below.Id, 5, safetyStock: 10),
            NewInventory(zeroNoAlert.Id, 0, safetyStock: 0),
            NewInventory(normal.Id, 10, safetyStock: 10));
        await dbContext.SaveChangesAsync();

        var repository = new ReportQueryRepository(dbContext);
        var (items, total, summary) = await repository.GetStockBalanceAsync(null, null, null, 1, 20);

        var item = Assert.Single(items);
        Assert.Equal(1, total);
        Assert.Equal("分类一", item.CategoryName);
        Assert.Equal(3, item.ProductCount);
        Assert.Equal(15, item.TotalQuantity);
        Assert.Equal(1, item.ZeroStockCount);
        // 阈值 0 表示不提醒（与库存查询页同源）：sku-2 库存 0 但不计入低库存
        Assert.Equal(1, item.BelowSafetyCount);
        Assert.Equal(3, summary.ProductCount);
        Assert.Equal(15, summary.TotalQuantity);
        Assert.Equal(1, summary.BelowSafetyCount);
    }

    [Fact]
    public async Task 库存余额表_关键词与分类筛选应作用于商品集合()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var productA = NewProduct("sku-a", CategoryAId);
        var productB = NewProduct("sku-b", CategoryBId);
        dbContext.Categories.AddRange(NewCategory(CategoryAId, "分类一"), NewCategory(CategoryBId, "分类二"));
        dbContext.Products.AddRange(productA, productB);
        dbContext.Inventory.AddRange(NewInventory(productA.Id, 10), NewInventory(productB.Id, 20));
        await dbContext.SaveChangesAsync();

        var repository = new ReportQueryRepository(dbContext);

        var (byCategory, totalByCategory, summaryByCategory) = await repository.GetStockBalanceAsync(null, CategoryBId, null, 1, 20);
        Assert.Equal(1, totalByCategory);
        Assert.Equal("分类二", Assert.Single(byCategory).CategoryName);
        Assert.Equal(20, summaryByCategory.TotalQuantity);

        var (byKeyword, totalByKeyword, _) = await repository.GetStockBalanceAsync("sku-a", null, null, 1, 20);
        Assert.Equal(1, totalByKeyword);
        Assert.Equal("分类一", Assert.Single(byKeyword).CategoryName);
    }

    // ============================== 采购 / 销售汇总 ==============================

    [Fact]
    public async Task 采购汇总_往来维度应按供应商聚合入库与退货并过滤作废单据()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var supplierId = Guid.NewGuid();
        var product = NewProduct("sku-1", CategoryAId);
        dbContext.Categories.Add(NewCategory(CategoryAId, "分类一"));
        dbContext.Products.Add(product);

        var receipt = NewReceipt(supplierId, Start.AddDays(1), 1000m);
        var voidedReceipt = NewReceipt(supplierId, Start.AddDays(2), 5000m, OrderStatus.Voided);
        var purchaseReturn = NewReturn(supplierId, Start.AddDays(3), 300m);
        dbContext.PurchaseReceipts.AddRange(receipt, voidedReceipt);
        dbContext.PurchaseReceiptItems.AddRange(
            NewReceiptItem(receipt.Id, product.Id, 10, 50m),
            NewReceiptItem(receipt.Id, product.Id, 5, 100m),
            NewReceiptItem(voidedReceipt.Id, product.Id, 50, 100m));
        dbContext.PurchaseReturns.Add(purchaseReturn);
        dbContext.PurchaseReturnItems.Add(NewReturnItem(purchaseReturn.Id, product.Id, 3, 100m));
        await dbContext.SaveChangesAsync();

        var repository = new ReportQueryRepository(dbContext);
        var (items, total, summary) = await repository.GetPurchaseSummaryAsync(Start, End, null, false, 1, 20);

        var item = Assert.Single(items);
        Assert.Equal(1, total);
        Assert.Equal(supplierId, item.Key);
        Assert.Equal("供应商一", item.Name);
        Assert.Equal(1, item.OrderCount);
        Assert.Equal(15, item.InboundQuantity);
        Assert.Equal(1000m, item.InboundAmount);
        Assert.Equal(3, item.ReturnQuantity);
        Assert.Equal(300m, item.ReturnAmount);

        Assert.Equal(1, summary.OrderCount);
        Assert.Equal(15, summary.InboundQuantity);
        Assert.Equal(1000m, summary.InboundAmount);
        Assert.Equal(3, summary.ReturnQuantity);
        Assert.Equal(300m, summary.ReturnAmount);
    }

    [Fact]
    public async Task 采购汇总_商品维度应按商品聚合且单据数按单据去重()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var supplierId = Guid.NewGuid();
        var productA = NewProduct("sku-a", CategoryAId);
        var productB = NewProduct("sku-b", CategoryAId);
        dbContext.Categories.Add(NewCategory(CategoryAId, "分类一"));
        dbContext.Products.AddRange(productA, productB);

        // 同一商品出现在两张单据中：单据数应为 2 而不是明细行数
        var receiptA = NewReceipt(supplierId, Start.AddDays(1), 300m);
        var receiptB = NewReceipt(supplierId, Start.AddDays(2), 200m);
        dbContext.PurchaseReceipts.AddRange(receiptA, receiptB);
        dbContext.PurchaseReceiptItems.AddRange(
            NewReceiptItem(receiptA.Id, productA.Id, 3, 100m, productA.Name),
            NewReceiptItem(receiptB.Id, productA.Id, 2, 100m, productA.Name),
            NewReceiptItem(receiptB.Id, productB.Id, 1, 100m, productB.Name));
        await dbContext.SaveChangesAsync();

        var repository = new ReportQueryRepository(dbContext);
        var (items, total, _) = await repository.GetPurchaseSummaryAsync(Start, End, null, true, 1, 20);

        Assert.Equal(2, total);
        var itemA = items.Single(x => x.Name == "商品sku-a");
        Assert.Equal(productA.Id, itemA.Key);
        Assert.Equal("个", itemA.Unit);
        Assert.Equal(2, itemA.OrderCount);
        Assert.Equal(5, itemA.InboundQuantity);
        Assert.Equal(500m, itemA.InboundAmount);

        var itemB = items.Single(x => x.Name == "商品sku-b");
        Assert.Equal(1, itemB.OrderCount);
        Assert.Equal(100m, itemB.InboundAmount);
    }

    [Fact]
    public async Task 采购汇总_期间为左闭右开_结束时刻单据不计入()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var supplierId = Guid.NewGuid();
        var product = NewProduct("sku-1", CategoryAId);
        dbContext.Categories.Add(NewCategory(CategoryAId, "分类一"));
        dbContext.Products.Add(product);

        var inRange = NewReceipt(supplierId, Start, 100m);
        var atEnd = NewReceipt(supplierId, End, 999m);
        dbContext.PurchaseReceipts.AddRange(inRange, atEnd);
        dbContext.PurchaseReceiptItems.AddRange(
            NewReceiptItem(inRange.Id, product.Id, 1, 100m),
            NewReceiptItem(atEnd.Id, product.Id, 9, 111m));
        await dbContext.SaveChangesAsync();

        var repository = new ReportQueryRepository(dbContext);
        var (items, _, summary) = await repository.GetPurchaseSummaryAsync(Start, End, null, false, 1, 20);

        Assert.Equal(100m, Assert.Single(items).InboundAmount);
        Assert.Equal(100m, summary.InboundAmount);
    }

    [Fact]
    public async Task 销售汇总_往来维度应按客户聚合出库与退货并过滤作废单据()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var customerId = Guid.NewGuid();
        var product = NewProduct("sku-1", CategoryAId);
        dbContext.Categories.Add(NewCategory(CategoryAId, "分类一"));
        dbContext.Products.Add(product);

        var shipment = new SalesShipment
        {
            Id = Guid.NewGuid(),
            ShipmentNo = $"GI{Start:yyyyMMdd}0001",
            PartnerId = customerId,
            PartnerName = "客户一",
            OrderDate = Start.AddDays(1),
            TotalAmount = 900m,
            Status = OrderStatus.Normal,
            CreatedAt = Start.AddDays(1),
            UpdatedAt = Start.AddDays(1),
        };
        var voidedShipment = new SalesShipment
        {
            Id = Guid.NewGuid(),
            ShipmentNo = $"GI{Start:yyyyMMdd}0002",
            PartnerId = customerId,
            PartnerName = "客户一",
            OrderDate = Start.AddDays(2),
            TotalAmount = 7000m,
            Status = OrderStatus.Voided,
            CreatedAt = Start.AddDays(2),
            UpdatedAt = Start.AddDays(2),
        };
        var salesReturn = new SalesReturn
        {
            Id = Guid.NewGuid(),
            ReturnNo = $"SR{Start:yyyyMMdd}0001",
            PartnerId = customerId,
            PartnerName = "客户一",
            ReturnDate = Start.AddDays(3),
            TotalAmount = 150m,
            Status = OrderStatus.Normal,
            CreatedAt = Start.AddDays(3),
            UpdatedAt = Start.AddDays(3),
        };
        dbContext.SalesShipments.AddRange(shipment, voidedShipment);
        dbContext.SalesShipmentItems.AddRange(
            new SalesShipmentItem { Id = Guid.NewGuid(), ShipmentId = shipment.Id, ProductId = product.Id, ProductName = "商品一", Unit = "个", Quantity = 30, UnitPrice = 30m, Subtotal = 900m },
            new SalesShipmentItem { Id = Guid.NewGuid(), ShipmentId = voidedShipment.Id, ProductId = product.Id, ProductName = "商品一", Unit = "个", Quantity = 70, UnitPrice = 100m, Subtotal = 7000m });
        dbContext.SalesReturns.Add(salesReturn);
        dbContext.SalesReturnItems.Add(
            new SalesReturnItem { Id = Guid.NewGuid(), ReturnId = salesReturn.Id, ProductId = product.Id, ProductName = "商品一", Unit = "个", Quantity = 5, UnitPrice = 30m, Subtotal = 150m });
        await dbContext.SaveChangesAsync();

        var repository = new ReportQueryRepository(dbContext);
        var (items, total, summary) = await repository.GetSalesSummaryAsync(Start, End, null, false, 1, 20);

        var item = Assert.Single(items);
        Assert.Equal(1, total);
        Assert.Equal(customerId, item.Key);
        Assert.Equal("客户一", item.Name);
        Assert.Equal(1, item.OrderCount);
        Assert.Equal(30, item.OutboundQuantity);
        Assert.Equal(900m, item.OutboundAmount);
        Assert.Equal(5, item.ReturnQuantity);
        Assert.Equal(150m, item.ReturnAmount);
        Assert.Equal(900m, summary.OutboundAmount);
        Assert.Equal(150m, summary.ReturnAmount);
    }

    [Fact]
    public async Task 销售汇总_商品维度应按商品聚合并支持客户筛选()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        var product = NewProduct("sku-1", CategoryAId);
        dbContext.Categories.Add(NewCategory(CategoryAId, "分类一"));
        dbContext.Products.Add(product);

        var shipmentA = new SalesShipment
        {
            Id = Guid.NewGuid(),
            ShipmentNo = $"GI{Start:yyyyMMdd}0001",
            PartnerId = customerA,
            PartnerName = "客户一",
            OrderDate = Start.AddDays(1),
            TotalAmount = 600m,
            Status = OrderStatus.Normal,
            CreatedAt = Start.AddDays(1),
            UpdatedAt = Start.AddDays(1),
        };
        var shipmentB = new SalesShipment
        {
            Id = Guid.NewGuid(),
            ShipmentNo = $"GI{Start:yyyyMMdd}0002",
            PartnerId = customerB,
            PartnerName = "客户二",
            OrderDate = Start.AddDays(2),
            TotalAmount = 400m,
            Status = OrderStatus.Normal,
            CreatedAt = Start.AddDays(2),
            UpdatedAt = Start.AddDays(2),
        };
        dbContext.SalesShipments.AddRange(shipmentA, shipmentB);
        dbContext.SalesShipmentItems.AddRange(
            new SalesShipmentItem { Id = Guid.NewGuid(), ShipmentId = shipmentA.Id, ProductId = product.Id, ProductName = "商品一", Unit = "个", Quantity = 6, UnitPrice = 100m, Subtotal = 600m },
            new SalesShipmentItem { Id = Guid.NewGuid(), ShipmentId = shipmentB.Id, ProductId = product.Id, ProductName = "商品一", Unit = "个", Quantity = 4, UnitPrice = 100m, Subtotal = 400m });
        await dbContext.SaveChangesAsync();

        var repository = new ReportQueryRepository(dbContext);

        var (allItems, _, allSummary) = await repository.GetSalesSummaryAsync(Start, End, null, true, 1, 20);
        Assert.Equal(10, Assert.Single(allItems).OutboundQuantity);
        Assert.Equal(1000m, allSummary.OutboundAmount);

        var (filteredItems, _, filteredSummary) = await repository.GetSalesSummaryAsync(Start, End, customerA, true, 1, 20);
        Assert.Equal(6, Assert.Single(filteredItems).OutboundQuantity);
        Assert.Equal(600m, filteredSummary.OutboundAmount);
    }
}
