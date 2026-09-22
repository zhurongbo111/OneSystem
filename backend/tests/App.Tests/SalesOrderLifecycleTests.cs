using App.Core;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.SalesOrders.CloseSalesOrder;
using App.Core.Features.SalesOrders.GetSalesOrderById;
using App.Core.Features.SalesOrders.GetSalesOrders;
using App.Core.Features.SalesOrders.UpdateSalesOrder;
using App.Core.Features.SalesOrders.VoidSalesOrder;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 销售订单生命周期测试（design.md §6，与采购订单同构）：编辑（仅待发货）/ 作废（仅待发货）/ 关闭（待发货 / 部分发货）、
/// 状态非法与已作废的错误码、详情累计量映射、分页筛选透传与候选订单过滤。
/// </summary>
public class SalesOrderLifecycleTests
{
    private static readonly DateTimeOffset OrderDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static SalesOrder NewOrder(OrderFlowStatus status = OrderFlowStatus.Pending, Guid partnerId = default)
        => new()
        {
            Id = Guid.NewGuid(),
            OrderNo = "SO202601010001",
            PartnerId = partnerId,
            PartnerName = "客户",
            OrderDate = OrderDate,
            TotalAmount = 10m,
            FlowStatus = status,
            CreatedAt = OrderDate,
            UpdatedAt = OrderDate,
        };

    private static SalesOrderItem NewItem(Guid orderId, int qty = 10, int fulfilled = 0, decimal price = 1m)
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

    private static (AppDbContext Context, FakeSalesOrderRepository Orders, StubCurrentUser User) CreateRepo()
    {
        var context = TestSupport.CreateDbContext();
        var user = new StubCurrentUser(Guid.NewGuid());
        return (context, new FakeSalesOrderRepository(), user);
    }

    // ============================== 编辑 ==============================

    [Fact]
    public async Task 编辑销售订单_待发货_应整体替换明细并重算金额()
    {
        var (context, orders, user) = CreateRepo();
        var partner = TestSupport.NewPartner("客户", type: PartnerType.Customer);
        var p1 = TestSupport.NewProduct("sku-su1", "商品一");
        context.Partners.Add(partner);
        context.Products.Add(p1);
        await context.SaveChangesAsync();

        var order = NewOrder(OrderFlowStatus.Pending, partner.Id);
        orders.Seed(order, new[] { NewItem(order.Id, 10) });

        var handler = new UpdateSalesOrderRequestHandler(
            orders, new PartnerRepository(context), new ProductRepository(context), new RecordingUnitOfWork(), user, TestSupport.AuditLogger);

        var result = await handler.HandleAsync(new UpdateSalesOrderRequest
        {
            Id = order.Id,
            PartnerId = partner.Id,
            OrderDate = OrderDate,
            Items = new[] { new UpdateSalesOrderItem { ProductId = p1.Id, Quantity = 7, UnitPrice = 2m } },
        });

        Assert.Equal(14m, result.TotalAmount);
        Assert.Single(result.Items);
        Assert.Equal(7, result.Items[0].Quantity);
        Assert.Equal(0, result.Items[0].FulfilledQuantity);
        Assert.Single(orders.ItemsOf(order.Id));
    }

    [Theory]
    [InlineData(OrderFlowStatus.Partial)]
    [InlineData(OrderFlowStatus.Completed)]
    [InlineData(OrderFlowStatus.Closed)]
    public async Task 编辑销售订单_非待发货_应报OrderStateInvalid(OrderFlowStatus status)
    {
        var (context, orders, user) = CreateRepo();
        var order = NewOrder(status);
        orders.Seed(order, new[] { NewItem(order.Id) });

        var handler = new UpdateSalesOrderRequestHandler(
            orders, new PartnerRepository(context), new ProductRepository(context), new RecordingUnitOfWork(), user, TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdateSalesOrderRequest
        {
            Id = order.Id,
            PartnerId = Guid.NewGuid(),
            OrderDate = OrderDate,
            Items = new[] { new UpdateSalesOrderItem { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 1m } },
        }));
        Assert.Equal(ErrorCode.OrderStateInvalid, ex.Code);
    }

    [Fact]
    public async Task 编辑销售订单_已作废_应报OrderVoided()
    {
        var (context, orders, user) = CreateRepo();
        var order = NewOrder(OrderFlowStatus.Voided);
        orders.Seed(order, new[] { NewItem(order.Id) });

        var handler = new UpdateSalesOrderRequestHandler(
            orders, new PartnerRepository(context), new ProductRepository(context), new RecordingUnitOfWork(), user, TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdateSalesOrderRequest
        {
            Id = order.Id,
            PartnerId = Guid.NewGuid(),
            OrderDate = OrderDate,
            Items = new[] { new UpdateSalesOrderItem { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 1m } },
        }));
        Assert.Equal(ErrorCode.OrderVoided, ex.Code);
    }

    [Fact]
    public async Task 编辑销售订单_不存在_应报NotFound()
    {
        var (context, orders, user) = CreateRepo();
        var handler = new UpdateSalesOrderRequestHandler(
            orders, new PartnerRepository(context), new ProductRepository(context), new RecordingUnitOfWork(), user, TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdateSalesOrderRequest
        {
            Id = Guid.NewGuid(),
            PartnerId = Guid.NewGuid(),
            OrderDate = OrderDate,
            Items = new[] { new UpdateSalesOrderItem { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 1m } },
        }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 作废 ==============================

    [Fact]
    public async Task 作废销售订单_待发货_应置已作废且不动累计量()
    {
        var (_, orders, user) = CreateRepo();
        var order = NewOrder(OrderFlowStatus.Pending);
        orders.Seed(order, new[] { NewItem(order.Id) });

        var result = await new VoidSalesOrderRequestHandler(orders, user, TestSupport.AuditLogger)
            .HandleAsync(new VoidSalesOrderRequest { Id = order.Id });

        Assert.Equal((int)OrderFlowStatus.Voided, result.FlowStatus);
        Assert.Equal(new[] { OrderFlowStatus.Voided }, orders.FlowStatusUpdates.ToArray());
        Assert.Empty(orders.FulfilledAdds);
    }

    [Fact]
    public async Task 作废销售订单_部分发货_应报OrderStateInvalid()
    {
        var (_, orders, user) = CreateRepo();
        var order = NewOrder(OrderFlowStatus.Partial);
        orders.Seed(order, new[] { NewItem(order.Id) });

        var ex = await Assert.ThrowsAsync<BusinessException>(() => new VoidSalesOrderRequestHandler(orders, user, TestSupport.AuditLogger)
            .HandleAsync(new VoidSalesOrderRequest { Id = order.Id }));
        Assert.Equal(ErrorCode.OrderStateInvalid, ex.Code);
    }

    [Fact]
    public async Task 作废销售订单_已作废_应报OrderVoided()
    {
        var (_, orders, user) = CreateRepo();
        var order = NewOrder(OrderFlowStatus.Voided);
        orders.Seed(order, new[] { NewItem(order.Id) });

        var ex = await Assert.ThrowsAsync<BusinessException>(() => new VoidSalesOrderRequestHandler(orders, user, TestSupport.AuditLogger)
            .HandleAsync(new VoidSalesOrderRequest { Id = order.Id }));
        Assert.Equal(ErrorCode.OrderVoided, ex.Code);
    }

    // ============================== 关闭 ==============================

    [Theory]
    [InlineData(OrderFlowStatus.Pending)]
    [InlineData(OrderFlowStatus.Partial)]
    public async Task 关闭销售订单_待发货或部分发货_应置已关闭(OrderFlowStatus status)
    {
        var (_, orders, user) = CreateRepo();
        var order = NewOrder(status);
        orders.Seed(order, new[] { NewItem(order.Id) });

        var result = await new CloseSalesOrderRequestHandler(orders, user, TestSupport.AuditLogger)
            .HandleAsync(new CloseSalesOrderRequest { Id = order.Id });

        Assert.Equal((int)OrderFlowStatus.Closed, result.FlowStatus);
        Assert.Equal(new[] { OrderFlowStatus.Closed }, orders.FlowStatusUpdates.ToArray());
    }

    [Theory]
    [InlineData(OrderFlowStatus.Completed)]
    [InlineData(OrderFlowStatus.Closed)]
    public async Task 关闭销售订单_已完成或已关闭_应报OrderStateInvalid(OrderFlowStatus status)
    {
        var (_, orders, user) = CreateRepo();
        var order = NewOrder(status);
        orders.Seed(order, new[] { NewItem(order.Id) });

        var ex = await Assert.ThrowsAsync<BusinessException>(() => new CloseSalesOrderRequestHandler(orders, user, TestSupport.AuditLogger)
            .HandleAsync(new CloseSalesOrderRequest { Id = order.Id }));
        Assert.Equal(ErrorCode.OrderStateInvalid, ex.Code);
    }

    [Fact]
    public async Task 关闭销售订单_已作废_应报OrderVoided()
    {
        var (_, orders, user) = CreateRepo();
        var order = NewOrder(OrderFlowStatus.Voided);
        orders.Seed(order, new[] { NewItem(order.Id) });

        var ex = await Assert.ThrowsAsync<BusinessException>(() => new CloseSalesOrderRequestHandler(orders, user, TestSupport.AuditLogger)
            .HandleAsync(new CloseSalesOrderRequest { Id = order.Id }));
        Assert.Equal(ErrorCode.OrderVoided, ex.Code);
    }

    // ============================== 查询 ==============================

    [Fact]
    public async Task 销售订单详情_应含累计已发与未发数量()
    {
        var (_, orders, _) = CreateRepo();
        var order = NewOrder(OrderFlowStatus.Partial);
        orders.Seed(order, new[] { NewItem(order.Id, 10, 4) });

        var result = await new GetSalesOrderByIdRequestHandler(orders)
            .HandleAsync(new GetSalesOrderByIdRequest { Id = order.Id });

        Assert.Equal(4, result.Items[0].FulfilledQuantity);
        Assert.Equal(6, result.Items[0].RemainingQuantity);
    }

    [Fact]
    public async Task 销售订单详情_不存在_应报NotFound()
    {
        var (_, orders, _) = CreateRepo();
        var ex = await Assert.ThrowsAsync<BusinessException>(() => new GetSalesOrderByIdRequestHandler(orders)
            .HandleAsync(new GetSalesOrderByIdRequest { Id = Guid.NewGuid() }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 销售订单分页_应透传筛选并映射未发数量合计()
    {
        var (_, orders, _) = CreateRepo();
        var order = NewOrder(OrderFlowStatus.Partial);
        orders.PagedResult = (new[] { (order, 6) }, 1);

        var result = await new GetSalesOrdersRequestHandler(orders).HandleAsync(new GetSalesOrdersRequest
        {
            Keyword = "SO2026",
            PartnerId = order.PartnerId,
            FlowStatus = OrderFlowStatus.Partial,
            Page = 1,
            PageSize = 20,
        });

        var args = orders.LastPagedArgs;
        Assert.NotNull(args);
        Assert.Equal("SO2026", args.Value.Keyword);
        Assert.Equal(order.PartnerId, args.Value.PartnerId);
        Assert.Equal(OrderFlowStatus.Partial, args.Value.FlowStatus);
        Assert.Equal(1, args.Value.Page);
        Assert.Equal(20, args.Value.PageSize);

        Assert.Equal(1, result.Total);
        Assert.Equal(6, result.Items[0].UnfulfilledQuantity);
    }

    [Fact]
    public async Task 销售订单候选_应仅返回待发货与部分发货()
    {
        var (_, orders, _) = CreateRepo();
        var partnerId = Guid.NewGuid();
        orders.Seed(NewOrder(OrderFlowStatus.Pending, partnerId), Array.Empty<SalesOrderItem>());
        orders.Seed(NewOrder(OrderFlowStatus.Partial, partnerId), Array.Empty<SalesOrderItem>());
        orders.Seed(NewOrder(OrderFlowStatus.Completed, partnerId), Array.Empty<SalesOrderItem>());

        var picks = await orders.GetPicksAsync(partnerId);

        Assert.Equal(2, picks.Count);
        Assert.All(picks, o => Assert.True(o.FlowStatus is OrderFlowStatus.Pending or OrderFlowStatus.Partial));
    }
}
