using App.Core;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.PurchaseOrders.ClosePurchaseOrder;
using App.Core.Features.PurchaseOrders.GetPurchaseOrderById;
using App.Core.Features.PurchaseOrders.GetPurchaseOrders;
using App.Core.Features.PurchaseOrders.UpdatePurchaseOrder;
using App.Core.Features.PurchaseOrders.VoidPurchaseOrder;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 采购订单生命周期测试（design.md §6）：编辑（仅待收货）/ 作废（仅待收货）/ 关闭（待收货 / 部分收货）、
/// 状态非法与已作废的错误码、详情累计量映射、分页筛选透传与候选订单过滤。
/// </summary>
public class PurchaseOrderLifecycleTests
{
    private static readonly DateTimeOffset OrderDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static PurchaseOrder NewOrder(OrderFlowStatus status = OrderFlowStatus.Pending, Guid partnerId = default)
        => new()
        {
            Id = Guid.NewGuid(),
            OrderNo = "PO202601010001",
            PartnerId = partnerId,
            PartnerName = "供应商",
            OrderDate = OrderDate,
            TotalAmount = 10m,
            FlowStatus = status,
            CreatedAt = OrderDate,
            UpdatedAt = OrderDate,
        };

    private static PurchaseOrderItem NewItem(Guid orderId, int qty = 10, int fulfilled = 0, decimal price = 1m)
        => new()
        {
            Id = SequentialGuidGenerator.NewSequential(),
            OrderId = orderId,
            ProductId = Guid.NewGuid(),
            ProductName = "商品",
            Unit = "件",
            Quantity = qty,
            UnitPrice = price,
            Subtotal = qty * price,
            FulfilledQuantity = fulfilled,
        };

    private static (AppDbContext Context, FakePurchaseOrderRepository Orders, StubCurrentUser User) CreateRepo()
    {
        var context = TestSupport.CreateDbContext();
        var user = new StubCurrentUser(Guid.NewGuid());
        return (context, new FakePurchaseOrderRepository(), user);
    }

    // ============================== 编辑 ==============================

    [Fact]
    public async Task 编辑采购订单_待收货_应整体替换明细并重算金额()
    {
        var (context, orders, user) = CreateRepo();
        var partner = TestSupport.NewPartner("供应商");
        var p1 = TestSupport.NewProduct("sku-u1", "商品一");
        context.Partners.Add(partner);
        context.Products.Add(p1);
        await context.SaveChangesAsync();

        var order = NewOrder(OrderFlowStatus.Pending, partner.Id);
        orders.Seed(order, new[] { NewItem(order.Id, 10) });

        var handler = new UpdatePurchaseOrderRequestHandler(
            orders, new PartnerRepository(context), new ProductRepository(context), new RecordingUnitOfWork(), user, TestSupport.AuditLogger);

        var result = await handler.HandleAsync(new UpdatePurchaseOrderRequest
        {
            Id = order.Id,
            PartnerId = partner.Id,
            OrderDate = OrderDate,
            Items = new[] { new UpdatePurchaseOrderItem { ProductId = p1.Id, Quantity = 7, UnitPrice = 2m } },
        });

        Assert.Equal(14m, result.TotalAmount);
        Assert.Single(result.Items);
        Assert.Equal(7, result.Items[0].Quantity);
        Assert.Equal(0, result.Items[0].FulfilledQuantity);
        Assert.Single(orders.ItemsOf(order.Id)); // 明细全量替换
    }

    [Theory]
    [InlineData(OrderFlowStatus.Partial)]
    [InlineData(OrderFlowStatus.Completed)]
    [InlineData(OrderFlowStatus.Closed)]
    public async Task 编辑采购订单_非待收货_应报OrderStateInvalid(OrderFlowStatus status)
    {
        var (context, orders, user) = CreateRepo();
        var order = NewOrder(status);
        orders.Seed(order, new[] { NewItem(order.Id) });

        var handler = new UpdatePurchaseOrderRequestHandler(
            orders, new PartnerRepository(context), new ProductRepository(context), new RecordingUnitOfWork(), user, TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdatePurchaseOrderRequest
        {
            Id = order.Id,
            PartnerId = Guid.NewGuid(),
            OrderDate = OrderDate,
            Items = new[] { new UpdatePurchaseOrderItem { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 1m } },
        }));
        Assert.Equal(ErrorCode.OrderStateInvalid, ex.Code);
    }

    [Fact]
    public async Task 编辑采购订单_已作废_应报OrderVoided()
    {
        var (context, orders, user) = CreateRepo();
        var order = NewOrder(OrderFlowStatus.Voided);
        orders.Seed(order, new[] { NewItem(order.Id) });

        var handler = new UpdatePurchaseOrderRequestHandler(
            orders, new PartnerRepository(context), new ProductRepository(context), new RecordingUnitOfWork(), user, TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdatePurchaseOrderRequest
        {
            Id = order.Id,
            PartnerId = Guid.NewGuid(),
            OrderDate = OrderDate,
            Items = new[] { new UpdatePurchaseOrderItem { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 1m } },
        }));
        Assert.Equal(ErrorCode.OrderVoided, ex.Code);
    }

    [Fact]
    public async Task 编辑采购订单_不存在_应报NotFound()
    {
        var (context, orders, user) = CreateRepo();
        var handler = new UpdatePurchaseOrderRequestHandler(
            orders, new PartnerRepository(context), new ProductRepository(context), new RecordingUnitOfWork(), user, TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdatePurchaseOrderRequest
        {
            Id = Guid.NewGuid(),
            PartnerId = Guid.NewGuid(),
            OrderDate = OrderDate,
            Items = new[] { new UpdatePurchaseOrderItem { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 1m } },
        }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 作废 ==============================

    [Fact]
    public async Task 作废采购订单_待收货_应置已作废且不动累计量()
    {
        var (_, orders, user) = CreateRepo();
        var order = NewOrder(OrderFlowStatus.Pending);
        orders.Seed(order, new[] { NewItem(order.Id) });

        var result = await new VoidPurchaseOrderRequestHandler(orders, user, TestSupport.AuditLogger)
            .HandleAsync(new VoidPurchaseOrderRequest { Id = order.Id });

        Assert.Equal((int)OrderFlowStatus.Voided, result.FlowStatus);
        Assert.Equal(new[] { OrderFlowStatus.Voided }, orders.FlowStatusUpdates.ToArray());
        Assert.Empty(orders.FulfilledAdds); // 计划单据作废不涉及累计量回退
    }

    [Fact]
    public async Task 作废采购订单_部分收货_应报OrderStateInvalid()
    {
        var (_, orders, user) = CreateRepo();
        var order = NewOrder(OrderFlowStatus.Partial);
        orders.Seed(order, new[] { NewItem(order.Id) });

        var ex = await Assert.ThrowsAsync<BusinessException>(() => new VoidPurchaseOrderRequestHandler(orders, user, TestSupport.AuditLogger)
            .HandleAsync(new VoidPurchaseOrderRequest { Id = order.Id }));
        Assert.Equal(ErrorCode.OrderStateInvalid, ex.Code);
    }

    [Fact]
    public async Task 作废采购订单_已作废_应报OrderVoided()
    {
        var (_, orders, user) = CreateRepo();
        var order = NewOrder(OrderFlowStatus.Voided);
        orders.Seed(order, new[] { NewItem(order.Id) });

        var ex = await Assert.ThrowsAsync<BusinessException>(() => new VoidPurchaseOrderRequestHandler(orders, user, TestSupport.AuditLogger)
            .HandleAsync(new VoidPurchaseOrderRequest { Id = order.Id }));
        Assert.Equal(ErrorCode.OrderVoided, ex.Code);
    }

    // ============================== 关闭 ==============================

    [Theory]
    [InlineData(OrderFlowStatus.Pending)]
    [InlineData(OrderFlowStatus.Partial)]
    public async Task 关闭采购订单_待收货或部分收货_应置已关闭(OrderFlowStatus status)
    {
        var (_, orders, user) = CreateRepo();
        var order = NewOrder(status);
        orders.Seed(order, new[] { NewItem(order.Id) });

        var result = await new ClosePurchaseOrderRequestHandler(orders, user, TestSupport.AuditLogger)
            .HandleAsync(new ClosePurchaseOrderRequest { Id = order.Id });

        Assert.Equal((int)OrderFlowStatus.Closed, result.FlowStatus);
        Assert.Equal(new[] { OrderFlowStatus.Closed }, orders.FlowStatusUpdates.ToArray());
    }

    [Theory]
    [InlineData(OrderFlowStatus.Completed)]
    [InlineData(OrderFlowStatus.Closed)]
    public async Task 关闭采购订单_已完成或已关闭_应报OrderStateInvalid(OrderFlowStatus status)
    {
        var (_, orders, user) = CreateRepo();
        var order = NewOrder(status);
        orders.Seed(order, new[] { NewItem(order.Id) });

        var ex = await Assert.ThrowsAsync<BusinessException>(() => new ClosePurchaseOrderRequestHandler(orders, user, TestSupport.AuditLogger)
            .HandleAsync(new ClosePurchaseOrderRequest { Id = order.Id }));
        Assert.Equal(ErrorCode.OrderStateInvalid, ex.Code);
    }

    [Fact]
    public async Task 关闭采购订单_已作废_应报OrderVoided()
    {
        var (_, orders, user) = CreateRepo();
        var order = NewOrder(OrderFlowStatus.Voided);
        orders.Seed(order, new[] { NewItem(order.Id) });

        var ex = await Assert.ThrowsAsync<BusinessException>(() => new ClosePurchaseOrderRequestHandler(orders, user, TestSupport.AuditLogger)
            .HandleAsync(new ClosePurchaseOrderRequest { Id = order.Id }));
        Assert.Equal(ErrorCode.OrderVoided, ex.Code);
    }

    // ============================== 查询 ==============================

    [Fact]
    public async Task 采购订单详情_应含累计已收与未收数量()
    {
        var (_, orders, _) = CreateRepo();
        var order = NewOrder(OrderFlowStatus.Partial);
        orders.Seed(order, new[] { NewItem(order.Id, 10, 4) });

        var result = await new GetPurchaseOrderByIdRequestHandler(orders)
            .HandleAsync(new GetPurchaseOrderByIdRequest { Id = order.Id });

        Assert.Equal(4, result.Items[0].FulfilledQuantity);
        Assert.Equal(6, result.Items[0].RemainingQuantity);
    }

    [Fact]
    public async Task 采购订单详情_不存在_应报NotFound()
    {
        var (_, orders, _) = CreateRepo();
        var ex = await Assert.ThrowsAsync<BusinessException>(() => new GetPurchaseOrderByIdRequestHandler(orders)
            .HandleAsync(new GetPurchaseOrderByIdRequest { Id = Guid.NewGuid() }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 采购订单分页_应透传筛选并映射未收数量合计()
    {
        var (_, orders, _) = CreateRepo();
        var order = NewOrder(OrderFlowStatus.Partial);
        orders.PagedResult = (new[] { (order, 6) }, 1);

        var start = OrderDate;
        var end = OrderDate.AddDays(1);
        var result = await new GetPurchaseOrdersRequestHandler(orders).HandleAsync(new GetPurchaseOrdersRequest
        {
            Keyword = "PO2026",
            PartnerId = order.PartnerId,
            FlowStatus = OrderFlowStatus.Partial,
            Start = start,
            End = end,
            Page = 2,
            PageSize = 50,
        });

        var args = orders.LastPagedArgs;
        Assert.NotNull(args);
        Assert.Equal("PO2026", args.Value.Keyword);
        Assert.Equal(order.PartnerId, args.Value.PartnerId);
        Assert.Equal(OrderFlowStatus.Partial, args.Value.FlowStatus);
        Assert.Equal(start, args.Value.Start);
        Assert.Equal(end, args.Value.End);
        Assert.Equal(2, args.Value.Page);
        Assert.Equal(50, args.Value.PageSize);

        Assert.Equal(1, result.Total);
        Assert.Equal(6, result.Items[0].UnfulfilledQuantity);
        Assert.Equal((int)OrderFlowStatus.Partial, result.Items[0].FlowStatus);
    }

    [Fact]
    public async Task 采购订单候选_应仅返回待收货与部分收货()
    {
        var (_, orders, _) = CreateRepo();
        var partnerId = Guid.NewGuid();
        orders.Seed(NewOrder(OrderFlowStatus.Pending, partnerId), Array.Empty<PurchaseOrderItem>());
        orders.Seed(NewOrder(OrderFlowStatus.Partial, partnerId), Array.Empty<PurchaseOrderItem>());
        orders.Seed(NewOrder(OrderFlowStatus.Completed, partnerId), Array.Empty<PurchaseOrderItem>());
        orders.Seed(NewOrder(OrderFlowStatus.Pending, Guid.NewGuid()), Array.Empty<PurchaseOrderItem>()); // 其他供应商

        var picks = await orders.GetPicksAsync(partnerId);

        Assert.Equal(2, picks.Count);
        Assert.All(picks, o => Assert.True(o.FlowStatus is OrderFlowStatus.Pending or OrderFlowStatus.Partial));
    }
}
