using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Inventory.UpdateInventorySafetyStock;
using App.Core.Features.Products.CreateProduct;
using App.Core.Features.PurchaseReceipts.CreatePurchaseReceipt;
using App.Core.Features.PurchaseReturns.CreatePurchaseReturn;
using App.Core.Features.SalesReturns.CreateSalesReturn;
using App.Core.Features.SalesShipments.CreateSalesShipment;
using App.Core.Features.StockTakes.CreateStockTake;
using App.Infrastructure;
using App.Infrastructure.Persistence;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 多仓维度贯通测试（specs/038-erp-multi-warehouse tasks.md §5.2–§5.6）：
/// 库存按仓隔离、单据带仓与默认仓兜底、停用 / 不存在仓的拒绝、流水带仓与按仓对账、
/// 新建商品逐启用仓建行、仓级安全库存维护。
/// </summary>
public class MultiWarehouseTests
{
    private static readonly DateTimeOffset Date = new(2026, 9, 23, 0, 0, 0, TimeSpan.Zero);

    private static readonly Guid SecondWarehouseId = Guid.Parse("00000000-0000-0000-0000-0000000000bb");

    private static FakeWarehouseRepository NewWarehouses()
    {
        var warehouses = new FakeWarehouseRepository();
        warehouses.AddEnabled(SecondWarehouseId, "WH02", "上海仓");
        return warehouses;
    }

    private static (AppDbContext Context, Partner Partner, Product Product) NewContext(
        PartnerType partnerType = PartnerType.Supplier)
    {
        var context = TestSupport.CreateDbContext();
        var partner = TestSupport.NewPartner(name: "往来一", type: partnerType);
        var product = TestSupport.NewProduct(code: "sku-wh");
        context.Partners.Add(partner);
        context.Products.Add(product);
        context.SaveChanges();
        return (context, partner, product);
    }

    // ============================== 新建商品：逐启用仓建行 ==============================

    [Fact]
    public async Task 新增商品_应为每个启用仓建零库存行并继承商品阈值()
    {
        var context = TestSupport.CreateDbContext();
        var category = new Category { Id = Guid.NewGuid(), Name = "原材料", CreatedAt = Date };
        context.Categories.Add(category);
        context.Warehouses.Add(TestSupport.NewWarehouse(TestWarehouse.DefaultId, "DEFAULT", TestWarehouse.DefaultName, isDefault: true));
        context.Warehouses.Add(TestSupport.NewWarehouse(SecondWarehouseId, "WH02", "上海仓"));
        context.Warehouses.Add(TestSupport.NewWarehouse(code: "WH03", name: "停用仓", status: PartnerStatus.Disabled));
        await context.SaveChangesAsync();

        var handler = new CreateProductRequestHandler(
            new ProductRepository(context),
            new CategoryRepository(context),
            new WarehouseRepository(context),
            new InventoryRepository(context),
            new UnitOfWork(context),
            new StubCurrentUser(Guid.NewGuid()),
            TestSupport.AuditLogger);

        var created = await handler.HandleAsync(new CreateProductRequest
        {
            Code = "sku-new",
            Name = "新商品",
            CategoryId = category.Id,
            Unit = "个",
            PurchasePrice = 1m,
            SalePrice = 2m,
            SafetyStock = 7,
        });

        var productId = Guid.Parse(created.Id);
        var rows = context.Inventory.Where(i => i.ProductId == productId).ToList();
        // 仅两个启用仓各一行（停用仓不建行）
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(0, r.Quantity));
        Assert.All(rows, r => Assert.Equal(7, r.SafetyStock));
        Assert.Contains(rows, r => r.WarehouseId == TestWarehouse.DefaultId);
        Assert.Contains(rows, r => r.WarehouseId == SecondWarehouseId);
    }

    // ============================== 单据带仓：默认仓兜底 ==============================

    [Fact]
    public async Task 采购入库_不传仓_应落默认仓且流水带默认仓()
    {
        var (context, partner, product) = NewContext();
        var warehouses = NewWarehouses();
        var inventory = new FakeInventoryRepository();
        var movements = new FakeStockMovementRepository();
        var gl = GeneralLedgerStubs.Create();

        var handler = new CreatePurchaseReceiptRequestHandler(
            new FakePurchaseReceiptRepository(),
            new FakePurchaseOrderRepository(),
            new PartnerRepository(context),
            new ProductRepository(context),
            warehouses,
            inventory,
            movements,
            gl.Vouchers, gl.Mappings, gl.Periods, gl.Accounts,
            new RecordingUnitOfWork(),
            new StubCurrentUser(Guid.NewGuid()),
            TestSupport.AuditLogger);

        var result = await handler.HandleAsync(new CreatePurchaseReceiptRequest
        {
            PartnerId = partner.Id,
            OrderDate = Date,
            Items = [new CreatePurchaseReceiptItem { ProductId = product.Id, Quantity = 5, UnitPrice = 2m }],
        });

        Assert.Equal(TestWarehouse.DefaultId.ToString(), result.WarehouseId);
        Assert.Equal(TestWarehouse.DefaultName, result.WarehouseName);
        Assert.Equal(1, warehouses.DefaultQueries); // 走了默认仓兜底
        Assert.Equal(5, inventory.GetQuantity(product.Id, TestWarehouse.DefaultId));
        Assert.Equal(0, inventory.GetQuantity(product.Id, SecondWarehouseId));
        Assert.Equal(TestWarehouse.DefaultId, Assert.Single(movements.Appended).WarehouseId);
    }

    [Fact]
    public async Task 采购入库_指定停用仓_应报40123()
    {
        var (context, partner, product) = NewContext();
        var warehouses = NewWarehouses();
        var disabled = Guid.NewGuid();
        warehouses.Seed(TestSupport.NewWarehouse(disabled, "WH09", "停用仓", status: PartnerStatus.Disabled));
        var gl = GeneralLedgerStubs.Create();

        var handler = new CreatePurchaseReceiptRequestHandler(
            new FakePurchaseReceiptRepository(),
            new FakePurchaseOrderRepository(),
            new PartnerRepository(context),
            new ProductRepository(context),
            warehouses,
            new FakeInventoryRepository(),
            new FakeStockMovementRepository(),
            gl.Vouchers, gl.Mappings, gl.Periods, gl.Accounts,
            new RecordingUnitOfWork(),
            new StubCurrentUser(Guid.NewGuid()),
            TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreatePurchaseReceiptRequest
        {
            PartnerId = partner.Id,
            OrderDate = Date,
            WarehouseId = disabled,
            Items = [new CreatePurchaseReceiptItem { ProductId = product.Id, Quantity = 1, UnitPrice = 1m }],
        }));

        Assert.Equal(ErrorCode.WarehouseDisabled, ex.Code);
    }

    [Fact]
    public async Task 采购入库_指定不存在仓_应报40400()
    {
        var (context, partner, product) = NewContext();
        var gl = GeneralLedgerStubs.Create();

        var handler = new CreatePurchaseReceiptRequestHandler(
            new FakePurchaseReceiptRepository(),
            new FakePurchaseOrderRepository(),
            new PartnerRepository(context),
            new ProductRepository(context),
            NewWarehouses(),
            new FakeInventoryRepository(),
            new FakeStockMovementRepository(),
            gl.Vouchers, gl.Mappings, gl.Periods, gl.Accounts,
            new RecordingUnitOfWork(),
            new StubCurrentUser(Guid.NewGuid()),
            TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreatePurchaseReceiptRequest
        {
            PartnerId = partner.Id,
            OrderDate = Date,
            WarehouseId = Guid.NewGuid(),
            Items = [new CreatePurchaseReceiptItem { ProductId = product.Id, Quantity = 1, UnitPrice = 1m }],
        }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 销售出库：按仓隔离与按仓防超卖 ==============================

    private static CreateSalesShipmentRequestHandler CreateSalesHandler(
        AppDbContext context,
        FakeWarehouseRepository warehouses,
        FakeInventoryRepository inventory,
        FakeStockMovementRepository movements)
    {
        var gl = GeneralLedgerStubs.Create();
        return new CreateSalesShipmentRequestHandler(
            new FakeSalesShipmentRepository(),
            new FakeSalesOrderRepository(),
            new PartnerRepository(context),
            new ProductRepository(context),
            warehouses,
            inventory,
            movements,
            new FakeSettlementQueryRepository(),
            gl.Vouchers, gl.Mappings, gl.Periods, gl.Accounts,
            new RecordingUnitOfWork(),
            new StubCurrentUser(Guid.NewGuid()),
            TestSupport.AuditLogger);
    }

    [Fact]
    public async Task 销售出库_从指定仓扣减_另一仓不受影响且流水带该仓()
    {
        var (context, customer, product) = NewContext(PartnerType.Customer);
        var warehouses = NewWarehouses();
        var inventory = new FakeInventoryRepository();
        inventory.Seed(product.Id, TestWarehouse.DefaultId, 10);
        inventory.Seed(product.Id, SecondWarehouseId, 30);
        var movements = new FakeStockMovementRepository();

        var result = await CreateSalesHandler(context, warehouses, inventory, movements)
            .HandleAsync(new CreateSalesShipmentRequest
            {
                PartnerId = customer.Id,
                OrderDate = Date,
                WarehouseId = SecondWarehouseId,
                Items = [new CreateSalesShipmentItem { ProductId = product.Id, Quantity = 30, UnitPrice = 5m }],
            });

        Assert.Equal(SecondWarehouseId.ToString(), result.WarehouseId);
        Assert.Equal(10, inventory.GetQuantity(product.Id, TestWarehouse.DefaultId));
        Assert.Equal(0, inventory.GetQuantity(product.Id, SecondWarehouseId));
        var movement = Assert.Single(movements.Appended);
        Assert.Equal(SecondWarehouseId, movement.WarehouseId);
        Assert.Equal(-30, movement.Quantity);
    }

    [Fact]
    public async Task 销售出库_该仓不足但另一仓充足_仍应拒绝且提示含仓名()
    {
        var (context, customer, product) = NewContext(PartnerType.Customer);
        var warehouses = NewWarehouses();
        var inventory = new FakeInventoryRepository();
        inventory.Seed(product.Id, TestWarehouse.DefaultId, 5);
        inventory.Seed(product.Id, SecondWarehouseId, 100);
        var movements = new FakeStockMovementRepository();

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            CreateSalesHandler(context, warehouses, inventory, movements).HandleAsync(new CreateSalesShipmentRequest
            {
                PartnerId = customer.Id,
                OrderDate = Date,
                WarehouseId = TestWarehouse.DefaultId,
                Items = [new CreateSalesShipmentItem { ProductId = product.Id, Quantity = 10, UnitPrice = 5m }],
            }));

        Assert.Equal(ErrorCode.InsufficientStock, ex.Code);
        Assert.Contains(TestWarehouse.DefaultName, ex.Message, StringComparison.Ordinal);
        // 整单回滚：两仓库存均不变、无流水
        Assert.Equal(5, inventory.GetQuantity(product.Id, TestWarehouse.DefaultId));
        Assert.Equal(100, inventory.GetQuantity(product.Id, SecondWarehouseId));
        Assert.Empty(movements.Appended);
    }

    // ============================== 退货：入库仓 / 出库仓 ==============================

    [Fact]
    public async Task 销售退货_回增到指定入库仓()
    {
        var (context, customer, product) = NewContext(PartnerType.Customer);
        var warehouses = NewWarehouses();
        var inventory = new FakeInventoryRepository();
        inventory.Seed(product.Id, SecondWarehouseId, 3);
        inventory.AverageCosts[product.Id] = 4m;
        var movements = new FakeStockMovementRepository();
        var gl = GeneralLedgerStubs.Create();

        var handler = new CreateSalesReturnRequestHandler(
            new FakeSalesReturnRepository(),
            new PartnerRepository(context),
            new ProductRepository(context),
            warehouses,
            inventory,
            movements,
            gl.Vouchers, gl.Mappings, gl.Periods, gl.Accounts,
            new RecordingUnitOfWork(),
            new StubCurrentUser(Guid.NewGuid()),
            TestSupport.AuditLogger);

        var result = await handler.HandleAsync(new CreateSalesReturnRequest
        {
            PartnerId = customer.Id,
            ReturnDate = Date,
            WarehouseId = SecondWarehouseId,
            Items = [new CreateSalesReturnItem { ProductId = product.Id, Quantity = 2, UnitPrice = 9m }],
        });

        Assert.Equal(SecondWarehouseId.ToString(), result.WarehouseId);
        Assert.Equal(5, inventory.GetQuantity(product.Id, SecondWarehouseId));
        Assert.Equal(0, inventory.GetQuantity(product.Id, TestWarehouse.DefaultId));
        Assert.Equal(SecondWarehouseId, Assert.Single(movements.Appended).WarehouseId);
    }

    [Fact]
    public async Task 采购退货_从指定出库仓扣减()
    {
        var (context, supplier, product) = NewContext();
        var warehouses = NewWarehouses();
        var inventory = new FakeInventoryRepository();
        inventory.Seed(product.Id, SecondWarehouseId, 8);
        inventory.AverageCosts[product.Id] = 3m;
        var movements = new FakeStockMovementRepository();
        var gl = GeneralLedgerStubs.Create();

        var handler = new CreatePurchaseReturnRequestHandler(
            new FakePurchaseReturnRepository(),
            new PartnerRepository(context),
            new ProductRepository(context),
            warehouses,
            inventory,
            movements,
            gl.Vouchers, gl.Mappings, gl.Periods, gl.Accounts,
            new RecordingUnitOfWork(),
            new StubCurrentUser(Guid.NewGuid()),
            TestSupport.AuditLogger);

        var result = await handler.HandleAsync(new CreatePurchaseReturnRequest
        {
            PartnerId = supplier.Id,
            ReturnDate = Date,
            WarehouseId = SecondWarehouseId,
            Items = [new CreatePurchaseReturnItem { ProductId = product.Id, Quantity = 3, UnitPrice = 3m }],
        });

        Assert.Equal(SecondWarehouseId.ToString(), result.WarehouseId);
        Assert.Equal(5, inventory.GetQuantity(product.Id, SecondWarehouseId));
        Assert.Equal(SecondWarehouseId, Assert.Single(movements.Appended).WarehouseId);
    }

    // ============================== 盘点：账面与差异只作用于所选仓 ==============================

    [Fact]
    public async Task 盘点_账面取所选仓_差异只影响该仓()
    {
        var (_, _, product) = NewContext();
        var warehouses = NewWarehouses();
        var inventory = new FakeInventoryRepository();
        inventory.Seed(product.Id, TestWarehouse.DefaultId, 10);
        inventory.Seed(product.Id, SecondWarehouseId, 4);
        var movements = new FakeStockMovementRepository();
        var products = new FakeProductRepository();
        products.ById[product.Id] = product;

        var handler = new CreateStockTakeRequestHandler(
            new FakeStockTakeRepository(),
            products,
            warehouses,
            inventory,
            movements,
            new RecordingUnitOfWork(),
            new StubCurrentUser(Guid.NewGuid()),
            TestSupport.AuditLogger);

        var result = await handler.HandleAsync(new CreateStockTakeRequest
        {
            Type = StockTakeType.Take,
            TakeDate = Date,
            WarehouseId = SecondWarehouseId,
            Items = [new CreateStockTakeItem { ProductId = product.Id, ActualQuantity = 6 }],
        });

        Assert.Equal(SecondWarehouseId.ToString(), result.WarehouseId);
        // 账面取所选仓（4），差异 +2
        var item = Assert.Single(result.Items);
        Assert.Equal(4, item.BookQuantity);
        Assert.Equal(2, item.Difference);
        Assert.Equal(6, inventory.GetQuantity(product.Id, SecondWarehouseId));
        Assert.Equal(10, inventory.GetQuantity(product.Id, TestWarehouse.DefaultId)); // 另一仓不变
        Assert.Equal(SecondWarehouseId, Assert.Single(movements.Appended).WarehouseId);
    }

    [Fact]
    public async Task 期初建账_仅当所选仓无流水_才允许()
    {
        var (_, _, product) = NewContext();
        var warehouses = NewWarehouses();
        var inventory = new FakeInventoryRepository();
        var movements = new FakeStockMovementRepository();
        // 默认仓已发生过变动，另一仓干净
        movements.Appended.Add(new StockMovement
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            WarehouseId = TestWarehouse.DefaultId,
            MovementType = StockMovementType.PurchaseInbound,
            Quantity = 1,
            CreatedAt = Date,
        });
        var products = new FakeProductRepository();
        products.ById[product.Id] = product;

        var handler = new CreateStockTakeRequestHandler(
            new FakeStockTakeRepository(), products, warehouses, inventory, movements,
            new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), TestSupport.AuditLogger);

        // 默认仓：拒绝（该仓已有变动）
        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateStockTakeRequest
        {
            Type = StockTakeType.Initial,
            TakeDate = Date,
            WarehouseId = TestWarehouse.DefaultId,
            Items = [new CreateStockTakeItem { ProductId = product.Id, ActualQuantity = 1, UnitCost = 1m }],
        }));
        Assert.Equal(ErrorCode.StockInitialNotAllowed, ex.Code);

        // 另一仓：允许（该仓从未发生变动）
        var ok = await handler.HandleAsync(new CreateStockTakeRequest
        {
            Type = StockTakeType.Initial,
            TakeDate = Date,
            WarehouseId = SecondWarehouseId,
            Items = [new CreateStockTakeItem { ProductId = product.Id, ActualQuantity = 3, UnitCost = 1m }],
        });
        Assert.Equal(SecondWarehouseId.ToString(), ok.WarehouseId);
    }

    // ============================== 对账：Σ 流水 == 该仓库存 ==============================

    [Fact]
    public async Task 按仓对账_任一商品仓的流水合计应等于该仓库存_组织级合计不变()
    {
        var context = TestSupport.CreateDbContext();
        var supplier = TestSupport.NewPartner(name: "供应商甲", type: PartnerType.Supplier);
        var customer = TestSupport.NewPartner(name: "客户乙", type: PartnerType.Customer);
        var product = TestSupport.NewProduct(code: "sku-rec");
        context.Partners.AddRange(supplier, customer);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var warehouses = NewWarehouses();
        var inventory = new FakeInventoryRepository();
        var movements = new FakeStockMovementRepository();
        var gl = GeneralLedgerStubs.Create();

        // 默认仓入库 10、上海仓入库 30、上海仓再出库 12
        var receiptHandler = new CreatePurchaseReceiptRequestHandler(
            new FakePurchaseReceiptRepository(), new FakePurchaseOrderRepository(),
            new PartnerRepository(context), new ProductRepository(context), warehouses,
            inventory, movements, gl.Vouchers, gl.Mappings, gl.Periods, gl.Accounts,
            new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), TestSupport.AuditLogger);
        await receiptHandler.HandleAsync(new CreatePurchaseReceiptRequest
        {
            PartnerId = supplier.Id,
            OrderDate = Date,
            Items = [new CreatePurchaseReceiptItem { ProductId = product.Id, Quantity = 10, UnitPrice = 2m }],
        });
        await receiptHandler.HandleAsync(new CreatePurchaseReceiptRequest
        {
            PartnerId = supplier.Id,
            OrderDate = Date,
            WarehouseId = SecondWarehouseId,
            Items = [new CreatePurchaseReceiptItem { ProductId = product.Id, Quantity = 30, UnitPrice = 2m }],
        });
        await CreateSalesHandler(context, warehouses, inventory, movements)
            .HandleAsync(new CreateSalesShipmentRequest
            {
                PartnerId = customer.Id,
                OrderDate = Date,
                WarehouseId = SecondWarehouseId,
                Items = [new CreateSalesShipmentItem { ProductId = product.Id, Quantity = 12, UnitPrice = 5m }],
            });

        // 按仓对账：任一「商品 × 仓」满足 Σ 流水 == 该仓库存
        foreach (var warehouseId in new[] { TestWarehouse.DefaultId, SecondWarehouseId })
        {
            var ledger = await movements.SumQuantityAsync(product.Id, warehouseId);
            Assert.Equal(inventory.GetQuantity(product.Id, warehouseId), ledger);
        }

        // 组织级汇总 = 各仓合计（028 / 026 口径不被破坏）
        var organization = await movements.SumQuantityAsync(product.Id);
        Assert.Equal(
            inventory.GetQuantity(product.Id, TestWarehouse.DefaultId) + inventory.GetQuantity(product.Id, SecondWarehouseId),
            organization);
        Assert.Equal(28, organization);
    }

    // ============================== 仓级安全库存维护 ==============================

    [Fact]
    public async Task 维护仓级安全库存_成功_返回行状态()
    {
        var (_, _, product) = NewContext();
        var warehouses = NewWarehouses();
        var inventory = new FakeInventoryRepository();
        inventory.Seed(product.Id, SecondWarehouseId, 2);

        var inventoryProducts = new FakeProductRepository();
        inventoryProducts.ById[product.Id] = product;
        var handler = new UpdateInventorySafetyStockRequestHandler(
            inventory, inventoryProducts, warehouses, new RecordingAuditLogger());

        var result = await handler.HandleAsync(new UpdateInventorySafetyStockRequest
        {
            ProductId = product.Id,
            WarehouseId = SecondWarehouseId,
            SafetyStock = 5,
        });

        Assert.Equal(5, result.SafetyStock);
        Assert.Equal(2, result.StockQuantity);
        Assert.True(result.IsBelowSafetyStock);
        Assert.Contains((product.Id, SecondWarehouseId, 5), inventory.SafetyStockWrites);
    }

    [Fact]
    public async Task 维护仓级安全库存_该仓无库存行_应报40400()
    {
        var (_, _, product) = NewContext();
        var inventoryProducts = new FakeProductRepository();
        inventoryProducts.ById[product.Id] = product;
        var handler = new UpdateInventorySafetyStockRequestHandler(
            new FakeInventoryRepository(),
            inventoryProducts,
            NewWarehouses(),
            new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdateInventorySafetyStockRequest
        {
            ProductId = product.Id,
            WarehouseId = SecondWarehouseId,
            SafetyStock = 1,
        }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }
}
