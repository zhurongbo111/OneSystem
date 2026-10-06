using App.Core;
using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Approvals;
using App.Core.Features.Approvals.ApproveOrder;
using App.Core.Features.Approvals.GetApprovals;
using App.Core.Features.Approvals.RejectApproval;
using App.Core.Features.Approvals.WithdrawApproval;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 审批动作用例测试（specs/042-erp-approval tasks 4.2 / 4.3）：
/// 通过（生效 + 状态流转 + 通知提交人）、驳回（单据作废、不动库存）、撤回（仅本人）；
/// 自审 40137、非待审批 40136、记录不存在 40400、单据已作废 40104、
/// 生效失败（库存不足 40103）整体回滚且单据与记录均保持待审批。
/// </summary>
public class ApprovalDecisionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);

    /// <summary>审计日志桩：只记录条目（本用例断言状态流转，不校验摘要文本）</summary>
    private sealed class AuditStub : IAuditLogger
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class Harness
    {
        public required AppDbContext Context { get; init; }

        public required List<string> Calls { get; init; }

        public FakePurchaseReceiptRepository Receipts { get; } = new();
        public FakeSalesShipmentRepository Shipments { get; } = new();
        public FakePurchaseReturnRepository PurchaseReturns { get; } = new();
        public FakeSalesReturnRepository SalesReturns { get; } = new();
        public FakeInventoryRepository Inventory { get; } = new();
        public FakeStockMovementRepository Movements { get; } = new();
        public FakeApprovalRepository Approvals { get; } = new();
        public FakeNotificationWriter Notifications { get; } = new();
        public RecordingUnitOfWork Uow { get; set; } = null!;
        public AuditStub Audit { get; } = new();

        public Guid Submitter { get; } = Guid.NewGuid();
        public Guid Approver { get; } = Guid.NewGuid();
        public StubCurrentUser CurrentUser { get; set; } = null!;

        public ApprovalDetailBuilder DetailBuilder { get; set; } = null!;
        public ApprovalOrderCloser OrderCloser { get; set; } = null!;
        public PurchaseReceiptFulfillment PurchaseFulfillment { get; set; } = null!;
        public SalesShipmentFulfillment SalesFulfillment { get; set; } = null!;
        public PurchaseReturnFulfillment PurchaseReturnFulfillment { get; set; } = null!;
        public SalesReturnFulfillment SalesReturnFulfillment { get; set; } = null!;
    }

    private static Harness CreateHarness()
    {
        var context = TestSupport.CreateDbContext();
        var calls = new List<string>();
        var gl = GeneralLedgerStubs.Create();
        var h = new Harness
        {
            Context = context,
            Calls = calls,
            Uow = new RecordingUnitOfWork(calls),
            CurrentUser = new StubCurrentUser(Guid.Empty),
        };
        h.DetailBuilder = new ApprovalDetailBuilder(
            h.Receipts, h.Shipments, h.PurchaseReturns, h.SalesReturns, new UserRepository(context));
        h.OrderCloser = new ApprovalOrderCloser(h.Receipts, h.Shipments, h.PurchaseReturns, h.SalesReturns);
        h.PurchaseFulfillment = new PurchaseReceiptFulfillment(
            h.Inventory, h.Movements, new FakePurchaseOrderRepository(),
            gl.Vouchers, gl.Mappings, gl.Periods, gl.Accounts);
        h.SalesFulfillment = new SalesShipmentFulfillment(
            h.Inventory, h.Movements, new FakeSalesOrderRepository(),
            gl.Vouchers, gl.Mappings, gl.Periods, gl.Accounts);
        h.PurchaseReturnFulfillment = new PurchaseReturnFulfillment(
            h.Inventory, h.Movements, gl.Vouchers, gl.Mappings, gl.Periods, gl.Accounts);
        h.SalesReturnFulfillment = new SalesReturnFulfillment(
            h.Inventory, h.Movements, gl.Vouchers, gl.Mappings, gl.Periods, gl.Accounts);
        h.CurrentUser = new StubCurrentUser(h.Approver);
        return h;
    }

    private static ApproveOrderRequestHandler CreateApproveHandler(Harness h)
        => new(
            h.Approvals, h.Receipts, h.Shipments, h.PurchaseReturns, h.SalesReturns,
            h.PurchaseFulfillment, h.SalesFulfillment, h.PurchaseReturnFulfillment, h.SalesReturnFulfillment,
            h.DetailBuilder, ApprovalTestStubs.Notifier(h.Notifications), h.Uow, h.CurrentUser, h.Audit);

    private static RejectApprovalRequestHandler CreateRejectHandler(Harness h)
        => new(
            h.Approvals, h.OrderCloser, h.DetailBuilder,
            ApprovalTestStubs.Notifier(h.Notifications), h.Uow, h.CurrentUser, h.Audit);

    private static WithdrawApprovalRequestHandler CreateWithdrawHandler(Harness h)
        => new(h.Approvals, h.OrderCloser, h.DetailBuilder, h.Uow, h.CurrentUser, h.Audit);

    /// <summary>预置一张「待审批」的销售出库单（含明细）+ 审批记录，并落库提交人与审批人</summary>
    private static async Task<(SalesShipment Order, Product Product, Approval Approval)> SeedPendingShipmentAsync(
        Harness h, int stock)
    {
        var customer = TestSupport.NewPartner("客户甲", type: PartnerType.Customer);
        var product = TestSupport.NewProduct("sku-dec-1", "商品甲");
        var submitter = TestSupport.NewUser("submitter", "提交人");
        submitter.Id = h.Submitter;
        var approver = TestSupport.NewUser("approver", "审批人");
        approver.Id = h.Approver;
        h.Context.Partners.Add(customer);
        h.Context.Products.Add(product);
        h.Context.Users.AddRange(submitter, approver);
        await h.Context.SaveChangesAsync();

        h.Inventory.Seed(product.Id, stock);
        var order = new SalesShipment
        {
            Id = Guid.NewGuid(),
            ShipmentNo = "GI202603020001",
            PartnerId = customer.Id,
            PartnerName = customer.Name,
            WarehouseId = TestWarehouse.DefaultId,
            WarehouseName = "主仓",
            OrderDate = Now,
            TotalAmount = 1500m,
            SettledAmount = 0m,
            Status = OrderStatus.Normal,
            ApprovalStatus = ApprovalStatus.Pending,
            CreatedAt = Now,
            UpdatedAt = Now,
        };
        h.Shipments.Seed(order,
        [
            new SalesShipmentItem
            {
                Id = SequentialGuidGenerator.NewSequential(),
                ShipmentId = order.Id,
                ProductId = product.Id,
                ProductName = product.Name,
                Unit = product.Unit,
                Quantity = 10,
                UnitPrice = 150m,
                Subtotal = 1500m,
            },
        ]);

        var approval = new Approval
        {
            Id = Guid.NewGuid(),
            OrderType = SettlementOrderType.SalesOutbound,
            OrderId = order.Id,
            OrderNo = order.ShipmentNo,
            PartnerName = order.PartnerName,
            Amount = order.TotalAmount,
            Status = ApprovalStatus.Pending,
            SubmittedBy = h.Submitter,
            SubmittedAt = Now,
        };
        h.Approvals.Seed(approval);
        return (order, product, approval);
    }

    // ============================== 通过 ==============================

    [Fact]
    public async Task 审批通过_应生效并流转状态且通知提交人()
    {
        var h = CreateHarness();
        var (order, product, approval) = await SeedPendingShipmentAsync(h, stock: 100);

        var result = await CreateApproveHandler(h).HandleAsync(
            new ApproveOrderRequest { Id = approval.Id, Remark = "同意" });

        // 生效：库存扣减 + 流水；单据与记录状态流转
        Assert.Equal(90, h.Inventory.GetQuantity(product.Id));
        Assert.Single(h.Movements.Appended);
        Assert.Equal(ApprovalStatus.Approved, order.ApprovalStatus);
        Assert.Equal(ApprovalStatus.Approved, approval.Status);
        Assert.Equal(h.Approver, approval.DecidedBy);
        Assert.Equal("同意", approval.DecisionRemark);
        Assert.Equal((int)ApprovalStatus.Approved, result.Status);
        Assert.Contains("Commit", h.Calls);

        // 通知提交人（审批结果）
        var notification = Assert.Single(h.Notifications.Written);
        Assert.Equal(h.Submitter, notification.UserId);
        Assert.Equal(NotificationType.ApprovalDecided, notification.Type);
    }

    [Fact]
    public async Task 审批通过_记录非待审批_应报40136()
    {
        var h = CreateHarness();
        var (_, _, approval) = await SeedPendingShipmentAsync(h, stock: 100);
        approval.Status = ApprovalStatus.Approved;

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateApproveHandler(h).HandleAsync(new ApproveOrderRequest { Id = approval.Id }));

        Assert.Equal(ErrorCode.ApprovalStateInvalid, ex.Code);
        Assert.Empty(h.Movements.Appended);
    }

    [Fact]
    public async Task 审批通过_自审_应报40137()
    {
        var h = CreateHarness();
        var (_, _, approval) = await SeedPendingShipmentAsync(h, stock: 100);
        h.CurrentUser = new StubCurrentUser(h.Submitter); // 当前用户 = 提交人

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateApproveHandler(h).HandleAsync(new ApproveOrderRequest { Id = approval.Id }));

        Assert.Equal(ErrorCode.ApprovalSelfForbidden, ex.Code);
        Assert.Empty(h.Movements.Appended);
    }

    [Fact]
    public async Task 审批通过_记录不存在_应报40400()
    {
        var h = CreateHarness();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateApproveHandler(h).HandleAsync(new ApproveOrderRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 审批通过_单据已作废_应报40104且保持待审批()
    {
        var h = CreateHarness();
        var (order, _, approval) = await SeedPendingShipmentAsync(h, stock: 100);
        order.Status = OrderStatus.Voided;

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateApproveHandler(h).HandleAsync(new ApproveOrderRequest { Id = approval.Id }));

        Assert.Equal(ErrorCode.OrderVoided, ex.Code);
        Assert.Contains("Rollback", h.Calls);
        Assert.Equal(ApprovalStatus.Pending, approval.Status);
    }

    [Fact]
    public async Task 审批通过_库存不足_应报40103且保持待审批()
    {
        var h = CreateHarness();
        var (order, product, approval) = await SeedPendingShipmentAsync(h, stock: 1); // 需要 10

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateApproveHandler(h).HandleAsync(new ApproveOrderRequest { Id = approval.Id }));

        Assert.Equal(ErrorCode.InsufficientStock, ex.Code);
        Assert.Contains("Rollback", h.Calls);
        Assert.DoesNotContain("Commit", h.Calls);
        Assert.Equal(ApprovalStatus.Pending, approval.Status);        // 可重试或驳回
        Assert.Equal(ApprovalStatus.Pending, order.ApprovalStatus);
        Assert.Empty(h.Movements.Appended);
        Assert.Equal(1, h.Inventory.GetQuantity(product.Id));          // 无半截数据
    }

    // ============================== 驳回 ==============================

    [Fact]
    public async Task 审批驳回_应作废单据且不动库存()
    {
        var h = CreateHarness();
        var (order, product, approval) = await SeedPendingShipmentAsync(h, stock: 100);

        var result = await CreateRejectHandler(h).HandleAsync(
            new RejectApprovalRequest { Id = approval.Id, Remark = "金额有误" });

        Assert.Equal(OrderStatus.Voided, order.Status);
        Assert.Equal(ApprovalStatus.Rejected, order.ApprovalStatus);
        Assert.Equal(ApprovalStatus.Rejected, approval.Status);
        Assert.Equal("金额有误", approval.DecisionRemark);
        Assert.Equal((int)ApprovalStatus.Rejected, result.Status);
        Assert.Empty(h.Inventory.Decrements);
        Assert.Empty(h.Movements.Appended);
        Assert.Equal(100, h.Inventory.GetQuantity(product.Id));

        // 结果通知提交人
        var notification = Assert.Single(h.Notifications.Written);
        Assert.Equal(h.Submitter, notification.UserId);
        Assert.Equal(NotificationType.ApprovalDecided, notification.Type);
    }

    [Fact]
    public async Task 审批驳回_自审_应报40137()
    {
        var h = CreateHarness();
        var (_, _, approval) = await SeedPendingShipmentAsync(h, stock: 100);
        h.CurrentUser = new StubCurrentUser(h.Submitter);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateRejectHandler(h).HandleAsync(
            new RejectApprovalRequest { Id = approval.Id, Remark = "不批" }));

        Assert.Equal(ErrorCode.ApprovalSelfForbidden, ex.Code);
    }

    [Fact]
    public async Task 审批驳回_记录非待审批_应报40136()
    {
        var h = CreateHarness();
        var (_, _, approval) = await SeedPendingShipmentAsync(h, stock: 100);
        approval.Status = ApprovalStatus.Withdrawn;

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateRejectHandler(h).HandleAsync(
            new RejectApprovalRequest { Id = approval.Id, Remark = "不批" }));

        Assert.Equal(ErrorCode.ApprovalStateInvalid, ex.Code);
    }

    // ============================== 撤回 ==============================

    [Fact]
    public async Task 撤回_提交人本人_应作废单据且不动库存()
    {
        var h = CreateHarness();
        var (order, product, approval) = await SeedPendingShipmentAsync(h, stock: 100);
        h.CurrentUser = new StubCurrentUser(h.Submitter);

        var result = await CreateWithdrawHandler(h).HandleAsync(new WithdrawApprovalRequest { Id = approval.Id });

        Assert.Equal(OrderStatus.Voided, order.Status);
        Assert.Equal(ApprovalStatus.Withdrawn, order.ApprovalStatus);
        Assert.Equal(ApprovalStatus.Withdrawn, approval.Status);
        Assert.Equal((int)ApprovalStatus.Withdrawn, result.Status);
        Assert.Empty(h.Inventory.Decrements);
        Assert.Empty(h.Movements.Appended);
        Assert.Equal(100, h.Inventory.GetQuantity(product.Id));
        Assert.Empty(h.Notifications.Written); // 撤回是本人动作，不发信
    }

    [Fact]
    public async Task 撤回_非提交人_应报40400()
    {
        var h = CreateHarness();
        var (_, _, approval) = await SeedPendingShipmentAsync(h, stock: 100);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateWithdrawHandler(h).HandleAsync(new WithdrawApprovalRequest { Id = approval.Id }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
        Assert.Equal(ApprovalStatus.Pending, approval.Status);
    }

    // ============================== 列表 ==============================

    [Fact]
    public async Task 审批列表_应透传筛选并解析提交人显示名()
    {
        var h = CreateHarness();
        var (_, _, approval) = await SeedPendingShipmentAsync(h, stock: 100);
        h.Approvals.PagedItems = [approval];
        h.Approvals.PagedTotal = 1;

        var handler = new GetApprovalsRequestHandler(h.Approvals, new UserRepository(h.Context));
        var result = await handler.HandleAsync(new GetApprovalsRequest
        {
            Status = ApprovalStatus.Pending,
            OrderType = SettlementOrderType.SalesOutbound,
            Page = 1,
            PageSize = 20,
        });

        var query = Assert.Single(h.Approvals.PagedQueries);
        Assert.Equal(ApprovalStatus.Pending, query.Status);
        Assert.Equal(SettlementOrderType.SalesOutbound, query.OrderType);
        var row = Assert.Single(result.Items);
        Assert.Equal((int)SettlementOrderType.SalesOutbound, row.OrderType);
        Assert.Equal("GI202603020001", row.OrderNo);
        Assert.Equal((int)ApprovalStatus.Pending, row.Status);
        Assert.Equal("提交人", row.SubmittedByName);
    }
}
