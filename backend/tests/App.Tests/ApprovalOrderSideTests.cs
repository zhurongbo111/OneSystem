using App.Core;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.PurchaseReceipts.GetPurchaseReceipts;
using App.Core.Features.PurchaseReceipts.VoidPurchaseReceipt;
using App.Core.Features.PurchaseReturns.GetPurchaseReturns;
using App.Core.Features.PurchaseReturns.VoidPurchaseReturn;
using App.Core.Features.SalesReturns.GetSalesReturns;
using App.Core.Features.SalesReturns.VoidSalesReturn;
using App.Core.Features.SalesShipments.GetSalesShipments;
using App.Core.Features.SalesShipments.VoidSalesShipment;

namespace App.Tests;

/// <summary>
/// 单据侧改造测试（specs/042-erp-approval tasks 4.5）：
/// ① 待审批单据禁止作废（`PUT .../void` → 40136，只能走审批 / 驳回 / 撤回）；
/// ② 四类单据列表按 <c>approvalStatus</c> 筛选透传仓储，且列表 / 详情出参带审批状态。
/// </summary>
public class ApprovalOrderSideTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);

    private static PurchaseReceipt NewReceipt(ApprovalStatus status) => new()
    {
        Id = Guid.NewGuid(),
        ReceiptNo = "GR202603020001",
        PartnerName = "供应商甲",
        WarehouseId = TestWarehouse.DefaultId,
        WarehouseName = "主仓",
        OrderDate = Now,
        TotalAmount = 1500m,
        Status = OrderStatus.Normal,
        ApprovalStatus = status,
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    private static SalesShipment NewShipment(ApprovalStatus status) => new()
    {
        Id = Guid.NewGuid(),
        ShipmentNo = "GI202603020001",
        PartnerName = "客户甲",
        WarehouseId = TestWarehouse.DefaultId,
        WarehouseName = "主仓",
        OrderDate = Now,
        TotalAmount = 1500m,
        Status = OrderStatus.Normal,
        ApprovalStatus = status,
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    private static PurchaseReturn NewPurchaseReturn(ApprovalStatus status) => new()
    {
        Id = Guid.NewGuid(),
        ReturnNo = "PR202603020001",
        PartnerName = "供应商甲",
        WarehouseId = TestWarehouse.DefaultId,
        WarehouseName = "主仓",
        ReturnDate = Now,
        TotalAmount = 1500m,
        Status = OrderStatus.Normal,
        ApprovalStatus = status,
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    private static SalesReturn NewSalesReturn(ApprovalStatus status) => new()
    {
        Id = Guid.NewGuid(),
        ReturnNo = "SR202603020001",
        PartnerName = "客户甲",
        WarehouseId = TestWarehouse.DefaultId,
        WarehouseName = "主仓",
        ReturnDate = Now,
        TotalAmount = 1500m,
        Status = OrderStatus.Normal,
        ApprovalStatus = status,
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    [Fact]
    public async Task 作废采购入库单_待审批_应报40136()
    {
        var receipts = new FakePurchaseReceiptRepository();
        var order = NewReceipt(ApprovalStatus.Pending);
        receipts.Seed(order, []);

        var handler = new VoidPurchaseReceiptRequestHandler(
            receipts, new FakePurchaseOrderRepository(), new FakeInventoryRepository(),
            new FakeStockMovementRepository(), GeneralLedgerStubs.Create().Vouchers,
            GeneralLedgerStubs.Create().Periods, new RecordingUnitOfWork(),
            new StubCurrentUser(Guid.NewGuid()), TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new VoidPurchaseReceiptRequest { Id = order.Id }));

        Assert.Equal(ErrorCode.ApprovalStateInvalid, ex.Code);
        Assert.Equal(OrderStatus.Normal, order.Status);
    }

    [Fact]
    public async Task 作废销售出库单_待审批_应报40136()
    {
        var shipments = new FakeSalesShipmentRepository();
        var order = NewShipment(ApprovalStatus.Pending);
        shipments.Seed(order, []);

        var handler = new VoidSalesShipmentRequestHandler(
            shipments, new FakeSalesOrderRepository(), new FakeInventoryRepository(),
            new FakeStockMovementRepository(), GeneralLedgerStubs.Create().Vouchers,
            GeneralLedgerStubs.Create().Periods, new RecordingUnitOfWork(),
            new StubCurrentUser(Guid.NewGuid()), TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new VoidSalesShipmentRequest { Id = order.Id }));

        Assert.Equal(ErrorCode.ApprovalStateInvalid, ex.Code);
        Assert.Equal(OrderStatus.Normal, order.Status);
    }

    [Fact]
    public async Task 作废采购退货单_待审批_应报40136()
    {
        var returns = new FakePurchaseReturnRepository();
        var order = NewPurchaseReturn(ApprovalStatus.Pending);
        returns.Seed(order, []);

        var handler = new VoidPurchaseReturnRequestHandler(
            returns, new FakeInventoryRepository(), new FakeStockMovementRepository(),
            GeneralLedgerStubs.Create().Vouchers, GeneralLedgerStubs.Create().Periods,
            new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new VoidPurchaseReturnRequest { Id = order.Id }));

        Assert.Equal(ErrorCode.ApprovalStateInvalid, ex.Code);
        Assert.Equal(OrderStatus.Normal, order.Status);
    }

    [Fact]
    public async Task 作废销售退货单_待审批_应报40136()
    {
        var returns = new FakeSalesReturnRepository();
        var order = NewSalesReturn(ApprovalStatus.Pending);
        returns.Seed(order, []);

        var handler = new VoidSalesReturnRequestHandler(
            returns, new FakeInventoryRepository(), new FakeStockMovementRepository(),
            GeneralLedgerStubs.Create().Vouchers, GeneralLedgerStubs.Create().Periods,
            new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new VoidSalesReturnRequest { Id = order.Id }));

        Assert.Equal(ErrorCode.ApprovalStateInvalid, ex.Code);
        Assert.Equal(OrderStatus.Normal, order.Status);
    }

    [Fact]
    public async Task 采购入库列表_应按审批状态筛选且出参带审批状态()
    {
        var receipts = new FakePurchaseReceiptRepository();
        var order = NewReceipt(ApprovalStatus.Pending);
        receipts.PagedItems = [(order, 10)];
        receipts.PagedTotal = 1;

        var handler = new GetPurchaseReceiptsRequestHandler(receipts);
        var result = await handler.HandleAsync(new GetPurchaseReceiptsRequest
        {
            ApprovalStatus = ApprovalStatus.Pending,
            Page = 1,
            PageSize = 20,
        });

        Assert.Equal(ApprovalStatus.Pending, Assert.Single(receipts.ApprovalStatusFilters));
        var row = Assert.Single(result.Items);
        Assert.Equal((int)ApprovalStatus.Pending, row.ApprovalStatus);
    }

    [Fact]
    public async Task 销售出库列表_应按审批状态筛选且出参带审批状态()
    {
        var shipments = new FakeSalesShipmentRepository();
        shipments.PagedItems = [(NewShipment(ApprovalStatus.Approved), 10)];
        shipments.PagedTotal = 1;

        var handler = new GetSalesShipmentsRequestHandler(shipments);
        var result = await handler.HandleAsync(new GetSalesShipmentsRequest
        {
            ApprovalStatus = ApprovalStatus.Approved,
            Page = 1,
            PageSize = 20,
        });

        Assert.Equal(ApprovalStatus.Approved, Assert.Single(shipments.ApprovalStatusFilters));
        Assert.Equal((int)ApprovalStatus.Approved, Assert.Single(result.Items).ApprovalStatus);
    }

    [Fact]
    public async Task 采购退货列表_应按审批状态筛选且出参带审批状态()
    {
        var returns = new FakePurchaseReturnRepository();
        returns.PagedItems = [NewPurchaseReturn(ApprovalStatus.Rejected)];
        returns.PagedTotal = 1;

        var handler = new GetPurchaseReturnsRequestHandler(returns);
        var result = await handler.HandleAsync(new GetPurchaseReturnsRequest
        {
            ApprovalStatus = ApprovalStatus.Rejected,
            Page = 1,
            PageSize = 20,
        });

        Assert.Equal(ApprovalStatus.Rejected, Assert.Single(returns.ApprovalStatusFilters));
        Assert.Equal((int)ApprovalStatus.Rejected, Assert.Single(result.Items).ApprovalStatus);
    }

    [Fact]
    public async Task 销售退货列表_应按审批状态筛选且出参带审批状态()
    {
        var returns = new FakeSalesReturnRepository();
        returns.PagedItems = [NewSalesReturn(ApprovalStatus.Withdrawn)];
        returns.PagedTotal = 1;

        var handler = new GetSalesReturnsRequestHandler(returns);
        var result = await handler.HandleAsync(new GetSalesReturnsRequest
        {
            ApprovalStatus = ApprovalStatus.Withdrawn,
            Page = 1,
            PageSize = 20,
        });

        Assert.Equal(ApprovalStatus.Withdrawn, Assert.Single(returns.ApprovalStatusFilters));
        Assert.Equal((int)ApprovalStatus.Withdrawn, Assert.Single(result.Items).ApprovalStatus);
    }
}
