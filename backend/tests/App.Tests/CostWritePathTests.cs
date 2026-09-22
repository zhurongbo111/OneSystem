using App.Core.Entities;
using App.Core.Features.PurchaseReceipts.CreatePurchaseReceipt;
using App.Core.Features.PurchaseReceipts.VoidPurchaseReceipt;
using App.Core.Features.SalesShipments.CreateSalesShipment;
using App.Core.Features.StockTakes.CreateStockTake;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 成本写入路径与口径测试（specs/026-erp-cost design.md §6）：
/// 供应商 / 商品用真实仓储（纯查询 InMemory 可用），单据 / 库存 / 流水 / 工作单元用行为型假实现
/// （规避 InMemory 不支持 <c>ExecuteUpdateAsync</c>）。
/// </summary>
public class CostWritePathTests
{
    private static readonly DateTimeOffset Date = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>构造共享的假实现与商品 / 往来种子</summary>
    private static async Task<(AppDbContext Context, Partner Supplier, Partner Customer, Product Product,
        FakeInventoryRepository Inventory, FakeStockMovementRepository Movements, List<string> Calls, RecordingUnitOfWork Uow,
        StubCurrentUser User)> CreateAsync()
    {
        var context = TestSupport.CreateDbContext();
        var calls = new List<string>();
        var supplier = TestSupport.NewPartner("成本供应商");
        var customer = TestSupport.NewPartner("成本客户", PartnerType.Customer);
        var product = TestSupport.NewProduct("sku-cost", "成本商品");
        context.Partners.AddRange(supplier, customer);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var inventory = new FakeInventoryRepository(calls);
        var movements = new FakeStockMovementRepository(calls);
        var uow = new RecordingUnitOfWork(calls);
        var user = new StubCurrentUser(Guid.NewGuid());

        return (context, supplier, customer, product, inventory, movements, calls, uow, user);
    }

    [Fact]
    public async Task 加权平均链路_入库加权_出库按均价结转_作废按原单价回冲()
    {
        var (context, supplier, customer, product, inventory, movements, _, uow, user) = await CreateAsync();
        var orders = new FakePurchaseReceiptRepository();
        var sales = new FakeSalesShipmentRepository();

        // 预置「期初建账 10 件 × 10 元」后的状态（期初用例本身见「期初成本」用例）
        inventory.Seed(product.Id, 10);
        inventory.CostAmounts[product.Id] = 100m;
        inventory.AverageCosts[product.Id] = 10m;

        var partnerRepository = new PartnerRepository(context);
        var productRepository = new ProductRepository(context);

        // 总账桩：自动凭证在本用例中只要求不阻断业务
        var gl = GeneralLedgerStubs.Create();

        // ① 采购入库 10 件 × 20 元 → 数量 20、金额 300、均价 15
        var createPurchase = new CreatePurchaseReceiptRequestHandler(
            orders, new FakePurchaseOrderRepository(), partnerRepository, productRepository, inventory, movements,
            gl.Vouchers, gl.Mappings, gl.Periods, gl.Accounts, uow, user, TestSupport.AuditLogger);
        await createPurchase.HandleAsync(new CreatePurchaseReceiptRequest
        {
            PartnerId = supplier.Id,
            OrderDate = Date,
            Items = [new CreatePurchaseReceiptItem { ProductId = product.Id, Quantity = 10, UnitPrice = 20m }],
        });

        Assert.Equal(20, inventory.GetQuantity(product.Id));
        Assert.Equal(300m, inventory.CostAmounts[product.Id]);
        Assert.Equal(15m, inventory.AverageCosts[product.Id]);
        var inbound = Assert.Single(movements.Appended);
        Assert.Equal(StockMovementType.PurchaseInbound, inbound.MovementType);
        Assert.Equal(20m, inbound.UnitCost);
        Assert.Equal(200m, inbound.TotalCost);

        // ② 销售出库 5 件 → 按变动前均价 15 结转，成本 75、金额 225、均价仍 15
        var createSale = new CreateSalesShipmentRequestHandler(
            sales, new FakeSalesOrderRepository(), partnerRepository, productRepository, inventory, movements,
            gl.Vouchers, gl.Mappings, gl.Periods, gl.Accounts, uow, user, TestSupport.AuditLogger);
        await createSale.HandleAsync(new CreateSalesShipmentRequest
        {
            PartnerId = customer.Id,
            OrderDate = Date,
            Items = [new CreateSalesShipmentItem { ProductId = product.Id, Quantity = 5, UnitPrice = 30m }],
        });

        Assert.Equal(15, inventory.GetQuantity(product.Id));
        Assert.Equal(225m, inventory.CostAmounts[product.Id]);
        Assert.Equal(15m, inventory.AverageCosts[product.Id]);
        Assert.Contains(product.Id, inventory.AverageCostReads); // 出库前先读均价
        var outbound = movements.Appended.Last();
        Assert.Equal(StockMovementType.SalesOutbound, outbound.MovementType);
        Assert.Equal(15m, outbound.UnitCost);
        Assert.Equal(-75m, outbound.TotalCost);

        // ③ 采购作废（回冲 −10 件）→ 按原入库单价 20 结转 −200，金额 25
        var voidPurchase = new VoidPurchaseReceiptRequestHandler(
            orders, new FakePurchaseOrderRepository(), inventory, movements, gl.Vouchers, gl.Periods, uow, user, TestSupport.AuditLogger);
        var receipt = Assert.Single(orders.Orders);
        await voidPurchase.HandleAsync(new VoidPurchaseReceiptRequest { Id = receipt.Id });

        var reversal = movements.Appended.Last();
        Assert.Equal(StockMovementType.PurchaseVoid, reversal.MovementType);
        Assert.Equal(20m, reversal.UnitCost);
        Assert.Equal(-200m, reversal.TotalCost);
        // 数量 = 期初 10 + 入库 10 − 出库 5 − 作废 10 = 5，金额 = 225 − 200 = 25
        Assert.Equal(5, inventory.GetQuantity(product.Id));
        Assert.Equal(25m, inventory.CostAmounts[product.Id]);
    }

    [Fact]
    public async Task 冲销还原成本_原流水缺失时按0计入并保留流水成本列为0()
    {
        var (context, supplier, _, product, inventory, movements, _, uow, user) = await CreateAsync();
        var orders = new FakePurchaseReceiptRepository();

        // 预置一张已入库的采购单（但**没有**对应的 PurchaseInbound 流水，模拟历史数据缺价）
        var receipt = new PurchaseReceipt
        {
            Id = Guid.NewGuid(),
            ReceiptNo = "GR202601010001",
            PartnerId = supplier.Id,
            PartnerName = supplier.Name,
            OrderDate = Date,
            TotalAmount = 200m,
            Status = OrderStatus.Normal,
        };
        orders.Seed(receipt, [new PurchaseReceiptItem
        {
            Id = Guid.NewGuid(),
            ReceiptId = receipt.Id,
            ProductId = product.Id,
            ProductName = product.Name,
            Unit = product.Unit,
            Quantity = 10,
            UnitPrice = 20m,
            Subtotal = 200m,
        }]);
        inventory.Seed(product.Id, 10);
        inventory.CostAmounts[product.Id] = 200m;
        inventory.AverageCosts[product.Id] = 20m;

        var gl = GeneralLedgerStubs.Create();
        var handler = new VoidPurchaseReceiptRequestHandler(
            orders, new FakePurchaseOrderRepository(), inventory, movements, gl.Vouchers, gl.Periods, uow, user, TestSupport.AuditLogger);
        await handler.HandleAsync(new VoidPurchaseReceiptRequest { Id = receipt.Id });

        var reversal = Assert.Single(movements.Appended);
        Assert.Equal(StockMovementType.PurchaseVoid, reversal.MovementType);
        Assert.Equal(0m, reversal.UnitCost);
        Assert.Equal(0m, reversal.TotalCost);
        // 缺价不阻断业务：数量照常回冲，成本额不变（0 结转）
        Assert.Equal(0, inventory.GetQuantity(product.Id));
    }

    [Fact]
    public async Task 期初建账成本_按录入单价加权且流水带成本列()
    {
        var (context, _, _, product, inventory, movements, _, uow, user) = await CreateAsync();

        var handler = new CreateStockTakeRequestHandler(
            new FakeStockTakeRepository(), new ProductRepository(context), inventory, movements, uow, user, TestSupport.AuditLogger);
        await handler.HandleAsync(new CreateStockTakeRequest
        {
            Type = StockTakeType.Initial,
            TakeDate = Date,
            Items = [new CreateStockTakeItem { ProductId = product.Id, ActualQuantity = 10, UnitCost = 10m }],
        });

        Assert.Equal(10, inventory.GetQuantity(product.Id));
        Assert.Equal(100m, inventory.CostAmounts[product.Id]);
        Assert.Equal(10m, inventory.AverageCosts[product.Id]);

        var movement = Assert.Single(movements.Appended);
        Assert.Equal(StockMovementType.InitialStock, movement.MovementType);
        Assert.Equal(10m, movement.UnitCost);
        Assert.Equal(100m, movement.TotalCost);
    }

    [Fact]
    public async Task 盘点成本_盘盈按均价入_盘亏按均价出_无差异行不动成本也不写流水()
    {
        var (context, _, _, product, inventory, movements, _, uow, user) = await CreateAsync();

        // 账面 10、均价 10、金额 100
        inventory.Seed(product.Id, 10);
        inventory.CostAmounts[product.Id] = 100m;
        inventory.AverageCosts[product.Id] = 10m;

        var handler = new CreateStockTakeRequestHandler(
            new FakeStockTakeRepository(), new ProductRepository(context), inventory, movements, uow, user, TestSupport.AuditLogger);

        // 无差异（实盘 = 账面）→ 回归 020 既有语义：不改库存、不写流水、不动成本
        await handler.HandleAsync(new CreateStockTakeRequest
        {
            Type = StockTakeType.Take,
            TakeDate = Date,
            Items = [new CreateStockTakeItem { ProductId = product.Id, ActualQuantity = 10 }],
        });

        Assert.Empty(movements.Appended);
        Assert.Empty(inventory.InboundCosts);
        Assert.Empty(inventory.OutboundCosts);
        Assert.Equal(100m, inventory.CostAmounts[product.Id]);

        // 盘盈 +2 → 金额 120、数量 12
        await handler.HandleAsync(new CreateStockTakeRequest
        {
            Type = StockTakeType.Take,
            TakeDate = Date,
            Items = [new CreateStockTakeItem { ProductId = product.Id, ActualQuantity = 12 }],
        });

        var gain = Assert.Single(movements.Appended);
        Assert.Equal(StockMovementType.StockTakeAdjust, gain.MovementType);
        Assert.Equal(10m, gain.UnitCost);
        Assert.Equal(20m, gain.TotalCost);
        Assert.Equal(120m, inventory.CostAmounts[product.Id]);

        // 盘亏 −5 → 金额 70、数量 7
        await handler.HandleAsync(new CreateStockTakeRequest
        {
            Type = StockTakeType.Take,
            TakeDate = Date,
            Items = [new CreateStockTakeItem { ProductId = product.Id, ActualQuantity = 7 }],
        });

        var loss = movements.Appended.Last();
        Assert.Equal(-5, loss.Quantity);
        Assert.Equal(10m, loss.UnitCost);
        Assert.Equal(-50m, loss.TotalCost);
        Assert.Equal(70m, inventory.CostAmounts[product.Id]);
    }
}
