using App.Core.Entities;
using App.Core.Features.PurchaseReceipts.CreatePurchaseReceipt;
using App.Core.Features.PurchaseReturns.CreatePurchaseReturn;
using App.Core.Features.SalesReturns.CreateSalesReturn;
using App.Core.Features.SalesShipments.CreateSalesShipment;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 创建路径审批闸门测试（specs/042-erp-approval tasks 4.1）：
/// **命中规则** → 单据落库为「待审批」+ 审批记录 + 给审批人发站内信，且**不产生任何库存 / 流水 / 成本变化**；
/// **未命中**（无规则 / 未启用 / 金额低于阈值）→ 保存即生效且不写审批记录（既有行为逐条不变）。
/// </summary>
public class ApprovalCreateGateTests
{
    private static readonly DateTimeOffset OrderDate = new(2026, 3, 2, 0, 0, 0, TimeSpan.Zero);

    // ============================== 采购入库 ==============================

    [Fact]
    public async Task 采购入库_命中阈值_应为待审批且不动库存()
    {
        using var context = TestSupport.CreateDbContext();
        var partner = TestSupport.NewPartner("供应商甲", type: PartnerType.Supplier);
        var product = TestSupport.NewProduct("sku-ap-po", "商品甲");
        context.Partners.Add(partner);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var receipts = new FakePurchaseReceiptRepository();
        var inventory = new FakeInventoryRepository();
        var movements = new FakeStockMovementRepository();
        var rules = new FakeApprovalRuleRepository();
        rules.Seed(SettlementOrderType.PurchaseInbound, 1000m, enabled: true);
        var approvals = new FakeApprovalRepository();
        var writer = new FakeNotificationWriter();
        var users = new FakePermissionedUserQuery();
        var approver = Guid.NewGuid();
        users.UserIds.Add(approver);
        var gl = GeneralLedgerStubs.Create();

        var handler = new CreatePurchaseReceiptRequestHandler(
            receipts, new FakePurchaseOrderRepository(), new PartnerRepository(context), new ProductRepository(context),
            new FakeWarehouseRepository(), new FakeBatchRepository(), rules, approvals,
            new PurchaseReceiptFulfillment(inventory, movements, new FakePurchaseOrderRepository(), gl.Vouchers, gl.Mappings, gl.Periods, gl.Accounts),
            ApprovalTestStubs.Notifier(writer, users), new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()),
            TestSupport.AuditLogger, new TestClock(OrderDate));

        var result = await handler.HandleAsync(new CreatePurchaseReceiptRequest
        {
            PartnerId = partner.Id,
            OrderDate = OrderDate,
            Items = [new CreatePurchaseReceiptItem { ProductId = product.Id, Quantity = 10, UnitPrice = 150m }],
        });

        // 单据待审批 + 审批记录（快照齐全）
        Assert.Equal((int)ApprovalStatus.Pending, result.ApprovalStatus);
        var approval = Assert.Single(approvals.Approvals);
        Assert.Equal(SettlementOrderType.PurchaseInbound, approval.OrderType);
        Assert.Equal(Guid.Parse(result.Id), approval.OrderId);
        Assert.Equal(result.ReceiptNo, approval.OrderNo);
        Assert.Equal(1500m, approval.Amount);
        Assert.Equal(ApprovalStatus.Pending, approval.Status);

        // 不产生任何库存 / 流水 / 成本变化
        Assert.Empty(inventory.Increments);
        Assert.Empty(inventory.InboundCosts);
        Assert.Empty(movements.Appended);

        // 站内信：发给具备审批权限的用户，可跳转审批页
        var notification = Assert.Single(writer.Written);
        Assert.Equal(approver, notification.UserId);
        Assert.Equal(NotificationType.ApprovalPending, notification.Type);
        Assert.Equal("approvals", notification.LinkRouteName);
        Assert.Contains(approval.Id.ToString(), notification.LinkQuery);
    }

    [Theory]
    [InlineData(false, 1000)]  // 规则未启用
    [InlineData(true, 2000)]   // 金额低于阈值
    public async Task 采购入库_未命中阈值_应保存即生效且不写审批记录(bool enabled, decimal threshold)
    {
        using var context = TestSupport.CreateDbContext();
        var partner = TestSupport.NewPartner("供应商乙", type: PartnerType.Supplier);
        var product = TestSupport.NewProduct("sku-ap-po2", "商品乙");
        context.Partners.Add(partner);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var receipts = new FakePurchaseReceiptRepository();
        var inventory = new FakeInventoryRepository();
        var movements = new FakeStockMovementRepository();
        var rules = new FakeApprovalRuleRepository();
        rules.Seed(SettlementOrderType.PurchaseInbound, threshold, enabled);
        var approvals = new FakeApprovalRepository();
        var writer = new FakeNotificationWriter();
        var gl = GeneralLedgerStubs.Create();

        var handler = new CreatePurchaseReceiptRequestHandler(
            receipts, new FakePurchaseOrderRepository(), new PartnerRepository(context), new ProductRepository(context),
            new FakeWarehouseRepository(), new FakeBatchRepository(), rules, approvals,
            new PurchaseReceiptFulfillment(inventory, movements, new FakePurchaseOrderRepository(), gl.Vouchers, gl.Mappings, gl.Periods, gl.Accounts),
            ApprovalTestStubs.Notifier(writer), new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()),
            TestSupport.AuditLogger, new TestClock(OrderDate));

        var result = await handler.HandleAsync(new CreatePurchaseReceiptRequest
        {
            PartnerId = partner.Id,
            OrderDate = OrderDate,
            Items = [new CreatePurchaseReceiptItem { ProductId = product.Id, Quantity = 10, UnitPrice = 100m }],
        });

        Assert.Equal((int)ApprovalStatus.None, result.ApprovalStatus);
        Assert.Empty(approvals.Approvals);
        Assert.Empty(writer.Written);

        // 保存即生效：库存 + 成本 + 流水一次到位
        Assert.Equal(10, inventory.GetQuantity(product.Id));
        Assert.Equal(1000m, inventory.CostAmounts[product.Id]);
        Assert.Single(movements.Appended);
    }

    [Fact]
    public async Task 采购入库_无规则_应保存即生效()
    {
        using var context = TestSupport.CreateDbContext();
        var partner = TestSupport.NewPartner("供应商丙", type: PartnerType.Supplier);
        var product = TestSupport.NewProduct("sku-ap-po3", "商品丙");
        context.Partners.Add(partner);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var inventory = new FakeInventoryRepository();
        var movements = new FakeStockMovementRepository();
        var approvals = new FakeApprovalRepository();
        var gl = GeneralLedgerStubs.Create();

        var handler = new CreatePurchaseReceiptRequestHandler(
            new FakePurchaseReceiptRepository(), new FakePurchaseOrderRepository(), new PartnerRepository(context),
            new ProductRepository(context), new FakeWarehouseRepository(), new FakeBatchRepository(),
            new FakeApprovalRuleRepository(), approvals,
            new PurchaseReceiptFulfillment(inventory, movements, new FakePurchaseOrderRepository(), gl.Vouchers, gl.Mappings, gl.Periods, gl.Accounts),
            ApprovalTestStubs.Notifier(), new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()),
            TestSupport.AuditLogger, new TestClock(OrderDate));

        var result = await handler.HandleAsync(new CreatePurchaseReceiptRequest
        {
            PartnerId = partner.Id,
            OrderDate = OrderDate,
            Items = [new CreatePurchaseReceiptItem { ProductId = product.Id, Quantity = 1, UnitPrice = 99999m }],
        });

        Assert.Equal((int)ApprovalStatus.None, result.ApprovalStatus);
        Assert.Empty(approvals.Approvals);
        Assert.Single(movements.Appended);
    }

    // ============================== 销售出库 ==============================

    [Fact]
    public async Task 销售出库_命中阈值_应为待审批且不动库存()
    {
        using var context = TestSupport.CreateDbContext();
        var partner = TestSupport.NewPartner("客户甲", type: PartnerType.Customer);
        var product = TestSupport.NewProduct("sku-ap-so", "商品甲");
        context.Partners.Add(partner);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var inventory = new FakeInventoryRepository();
        inventory.Seed(product.Id, 100);
        var movements = new FakeStockMovementRepository();
        var rules = new FakeApprovalRuleRepository();
        rules.Seed(SettlementOrderType.SalesOutbound, 1000m, enabled: true);
        var approvals = new FakeApprovalRepository();
        var gl = GeneralLedgerStubs.Create();

        var handler = new CreateSalesShipmentRequestHandler(
            new FakeSalesShipmentRepository(), new FakeSalesOrderRepository(), new PartnerRepository(context),
            new ProductRepository(context), new FakeWarehouseRepository(), new FakeSettlementQueryRepository(),
            new FakeBatchRepository(), rules, approvals,
            new SalesShipmentFulfillment(inventory, movements, new FakeSalesOrderRepository(), gl.Vouchers, gl.Mappings, gl.Periods, gl.Accounts),
            ApprovalTestStubs.Notifier(), new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()),
            TestSupport.AuditLogger, new TestClock(OrderDate));

        var result = await handler.HandleAsync(new CreateSalesShipmentRequest
        {
            PartnerId = partner.Id,
            OrderDate = OrderDate,
            Items = [new CreateSalesShipmentItem { ProductId = product.Id, Quantity = 10, UnitPrice = 200m }],
        });

        Assert.Equal((int)ApprovalStatus.Pending, result.ApprovalStatus);
        Assert.Single(approvals.Approvals);
        Assert.Empty(inventory.Decrements);
        Assert.Empty(movements.Appended);
        Assert.Equal(100, inventory.GetQuantity(product.Id)); // 库存未被占用
    }

    // ============================== 采购退货 ==============================

    [Fact]
    public async Task 采购退货_命中阈值_应为待审批且不动库存()
    {
        using var context = TestSupport.CreateDbContext();
        var partner = TestSupport.NewPartner("供应商丁", type: PartnerType.Supplier);
        var product = TestSupport.NewProduct("sku-ap-pr", "商品丁");
        context.Partners.Add(partner);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var inventory = new FakeInventoryRepository();
        inventory.Seed(product.Id, 5);
        var movements = new FakeStockMovementRepository();
        var rules = new FakeApprovalRuleRepository();
        rules.Seed(SettlementOrderType.PurchaseReturn, 100m, enabled: true);
        var approvals = new FakeApprovalRepository();
        var gl = GeneralLedgerStubs.Create();

        var handler = new CreatePurchaseReturnRequestHandler(
            new FakePurchaseReturnRepository(), new PartnerRepository(context), new ProductRepository(context),
            new FakeWarehouseRepository(), new FakeBatchRepository(), rules, approvals,
            new PurchaseReturnFulfillment(inventory, movements, gl.Vouchers, gl.Mappings, gl.Periods, gl.Accounts),
            ApprovalTestStubs.Notifier(), new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()),
            TestSupport.AuditLogger, new TestClock(OrderDate));

        var result = await handler.HandleAsync(new CreatePurchaseReturnRequest
        {
            PartnerId = partner.Id,
            ReturnDate = OrderDate,
            Items = [new CreatePurchaseReturnItem { ProductId = product.Id, Quantity = 5, UnitPrice = 20m }],
        });

        Assert.Equal((int)ApprovalStatus.Pending, result.ApprovalStatus);
        Assert.Single(approvals.Approvals);
        Assert.Empty(inventory.Decrements);
        Assert.Empty(movements.Appended);
        Assert.Equal(5, inventory.GetQuantity(product.Id));
    }

    // ============================== 销售退货 ==============================

    [Fact]
    public async Task 销售退货_命中阈值_应为待审批且不动库存()
    {
        using var context = TestSupport.CreateDbContext();
        var partner = TestSupport.NewPartner("客户乙", type: PartnerType.Customer);
        var product = TestSupport.NewProduct("sku-ap-sr", "商品戊");
        context.Partners.Add(partner);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var inventory = new FakeInventoryRepository();
        var movements = new FakeStockMovementRepository();
        var rules = new FakeApprovalRuleRepository();
        rules.Seed(SettlementOrderType.SalesReturn, 100m, enabled: true);
        var approvals = new FakeApprovalRepository();
        var gl = GeneralLedgerStubs.Create();

        var handler = new CreateSalesReturnRequestHandler(
            new FakeSalesReturnRepository(), new PartnerRepository(context), new ProductRepository(context),
            new FakeWarehouseRepository(), new FakeBatchRepository(), rules, approvals,
            new SalesReturnFulfillment(inventory, movements, gl.Vouchers, gl.Mappings, gl.Periods, gl.Accounts),
            ApprovalTestStubs.Notifier(), new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()),
            TestSupport.AuditLogger, new TestClock(OrderDate));

        var result = await handler.HandleAsync(new CreateSalesReturnRequest
        {
            PartnerId = partner.Id,
            ReturnDate = OrderDate,
            Items = [new CreateSalesReturnItem { ProductId = product.Id, Quantity = 5, UnitPrice = 20m }],
        });

        Assert.Equal((int)ApprovalStatus.Pending, result.ApprovalStatus);
        Assert.Single(approvals.Approvals);
        Assert.Empty(inventory.Increments);
        Assert.Empty(movements.Appended);
        Assert.Equal(0, inventory.GetQuantity(product.Id)); // 库存未回增
    }
}
