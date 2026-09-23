using App.Core;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.SalesShipments.CreateSalesShipment;
using App.Core.Features.SalesShipments.VoidSalesShipment;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 销售出库单关联订单测试（specs/024-erp-order-flow design.md §3.4 / §6，与采购入库单同构）：
/// 关联成功（累计已发回写 + 订单状态三态，且库存先扣后插单）；超量 40115 / 状态不允许 40116 /
/// 客户不一致 40117 / 明细不属于订单 40400 / 订单已作废 40104；不关联订单时不调用订单仓储；
/// 作废回退累计量并重算状态（订单已关闭不回退状态）。
/// </summary>
public class SalesShipmentOrderLinkTests
{
    private static readonly DateTimeOffset OrderDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class LinkedHarness
    {
        public required AppDbContext Context { get; init; }
        public required StubCurrentUser User { get; init; }
        public required FakeSalesShipmentRepository Shipments { get; init; }
        public required FakeSalesOrderRepository Orders { get; init; }
        public required FakeInventoryRepository Inventory { get; init; }
        public required FakeStockMovementRepository Movements { get; init; }
        public required RecordingUnitOfWork Uow { get; init; }
        public required CreateSalesShipmentRequestHandler Handler { get; init; }
        public required List<string> Calls { get; init; }
        public required Partner Partner { get; init; }
        public required Product Product { get; init; }
        public required SalesOrder Order { get; init; }
        public required SalesOrderItem OrderItem { get; init; }
    }

    private static async Task<LinkedHarness> CreateLinkedAsync(
        int orderedQuantity = 10,
        int fulfilledQuantity = 0,
        OrderFlowStatus flowStatus = OrderFlowStatus.Pending,
        Guid? orderPartnerId = null)
    {
        var context = TestSupport.CreateDbContext();
        var user = new StubCurrentUser(Guid.NewGuid());
        var calls = new List<string>();
        var shipments = new FakeSalesShipmentRepository(calls);
        var orders = new FakeSalesOrderRepository(calls);
        var inventory = new FakeInventoryRepository(calls);
        var movements = new FakeStockMovementRepository(calls);
        var uow = new RecordingUnitOfWork(calls);

        var partner = TestSupport.NewPartner("联动客户", type: PartnerType.Customer);
        var product = TestSupport.NewProduct("sku-slink", "联动商品", 8m);
        context.Partners.Add(partner);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var order = new SalesOrder
        {
            Id = Guid.NewGuid(),
            OrderNo = "SO202601010001",
            PartnerId = orderPartnerId ?? partner.Id,
            PartnerName = partner.Name,
            OrderDate = OrderDate,
            TotalAmount = orderedQuantity * 8m,
            FlowStatus = flowStatus,
            CreatedAt = OrderDate,
            UpdatedAt = OrderDate,
        };
        var orderItem = new SalesOrderItem
        {
            Id = SequentialGuidGenerator.NewSequential(),
            OrderId = order.Id,
            ProductId = product.Id,
            ProductName = product.Name,
            Unit = product.Unit,
            Quantity = orderedQuantity,
            UnitPrice = 8m,
            Subtotal = orderedQuantity * 8m,
            FulfilledQuantity = fulfilledQuantity,
        };
        orders.Seed(order, new[] { orderItem });

        inventory.Seed(product.Id, 100); // 保证默认库存充足，扣减不失败

        var gl = GeneralLedgerStubs.Create();
        var handler = new CreateSalesShipmentRequestHandler(
            shipments, orders, new PartnerRepository(context), new ProductRepository(context),
            inventory, movements, new FakeSettlementQueryRepository(),
            gl.Vouchers, gl.Mappings, gl.Periods, gl.Accounts, uow, user, TestSupport.AuditLogger);

        return new LinkedHarness
        {
            Context = context,
            User = user,
            Shipments = shipments,
            Orders = orders,
            Inventory = inventory,
            Movements = movements,
            Uow = uow,
            Handler = handler,
            Calls = calls,
            Partner = partner,
            Product = product,
            Order = order,
            OrderItem = orderItem,
        };
    }

    private static CreateSalesShipmentRequest LinkedRequest(LinkedHarness h, int quantity, Guid? orderItemId = null)
        => new()
        {
            PartnerId = h.Partner.Id,
            OrderDate = OrderDate,
            OrderId = h.Order.Id,
            Items = new[]
            {
                new CreateSalesShipmentItem
                {
                    ProductId = h.Product.Id,
                    Quantity = quantity,
                    UnitPrice = 8m,
                    OrderItemId = orderItemId ?? h.OrderItem.Id,
                },
            },
        };

    // ============================== 关联成功 ==============================

    [Fact]
    public async Task 关联订单_部分发货_应回写累计已发并置部分发货()
    {
        var h = await CreateLinkedAsync(orderedQuantity: 10);

        var result = await h.Handler.HandleAsync(LinkedRequest(h, 4));

        Assert.Equal(h.Order.Id.ToString(), result.OrderId);
        Assert.Equal(h.Order.OrderNo, result.OrderNo);
        Assert.Equal(h.OrderItem.Id.ToString(), result.Items[0].OrderItemId);

        Assert.Equal(new[] { (h.OrderItem.Id, 4) }, h.Orders.FulfilledAdds.ToArray());
        Assert.Equal(new[] { OrderFlowStatus.Partial }, h.Orders.FlowStatusUpdates.ToArray());
        Assert.Equal(96, h.Inventory.GetQuantity(h.Product.Id)); // 先扣库存 100 − 4
    }

    [Fact]
    public async Task 关联订单_发满_应置已完成()
    {
        var h = await CreateLinkedAsync(orderedQuantity: 10, fulfilledQuantity: 6);

        await h.Handler.HandleAsync(LinkedRequest(h, 4));

        Assert.Equal(new[] { OrderFlowStatus.Completed }, h.Orders.FlowStatusUpdates.ToArray());
        Assert.Equal(10, h.Orders.ItemsOf(h.Order.Id)[0].FulfilledQuantity);
    }

    [Fact]
    public async Task 不关联订单_不应调用订单仓储()
    {
        var h = await CreateLinkedAsync();

        await h.Handler.HandleAsync(new CreateSalesShipmentRequest
        {
            PartnerId = h.Partner.Id,
            OrderDate = OrderDate,
            Items = new[] { new CreateSalesShipmentItem { ProductId = h.Product.Id, Quantity = 2, UnitPrice = 8m } },
        });

        Assert.Empty(h.Orders.FulfilledAdds);
        Assert.Empty(h.Orders.FlowStatusUpdates);
        Assert.Equal(98, h.Inventory.GetQuantity(h.Product.Id));
    }

    // ============================== 关联异常分支 ==============================

    [Fact]
    public async Task 关联订单_超出未发数量_应报OrderFulfillExceeded且不扣库存()
    {
        var h = await CreateLinkedAsync(orderedQuantity: 10, fulfilledQuantity: 8);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => h.Handler.HandleAsync(LinkedRequest(h, 3)));

        Assert.Equal(ErrorCode.OrderFulfillExceeded, ex.Code);
        Assert.Contains("SO202601010001", ex.Message);
        Assert.Contains("未发 2", ex.Message);

        Assert.Empty(h.Inventory.Decrements);
        Assert.Empty(h.Movements.Appended);
        Assert.Empty(h.Orders.FulfilledAdds);
        Assert.DoesNotContain("Begin", h.Calls);
    }

    [Theory]
    [InlineData(OrderFlowStatus.Completed)]
    [InlineData(OrderFlowStatus.Closed)]
    public async Task 关联订单_已完成或已关闭_应报OrderStateInvalid(OrderFlowStatus status)
    {
        var h = await CreateLinkedAsync(flowStatus: status);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => h.Handler.HandleAsync(LinkedRequest(h, 1)));

        Assert.Equal(ErrorCode.OrderStateInvalid, ex.Code);
        Assert.Empty(h.Inventory.Decrements);
    }

    [Fact]
    public async Task 关联订单_订单已作废_应报OrderVoided()
    {
        var h = await CreateLinkedAsync(flowStatus: OrderFlowStatus.Voided);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => h.Handler.HandleAsync(LinkedRequest(h, 1)));

        Assert.Equal(ErrorCode.OrderVoided, ex.Code);
    }

    [Fact]
    public async Task 关联订单_客户不一致_应报OrderPartnerMismatch()
    {
        var h = await CreateLinkedAsync(orderPartnerId: Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => h.Handler.HandleAsync(LinkedRequest(h, 1)));

        Assert.Equal(ErrorCode.OrderPartnerMismatch, ex.Code);
    }

    [Fact]
    public async Task 关联订单_明细行不属于该订单_应报NotFound()
    {
        var h = await CreateLinkedAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => h.Handler.HandleAsync(LinkedRequest(h, 1, orderItemId: Guid.NewGuid())));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
        Assert.Empty(h.Inventory.Decrements);
    }

    // ============================== 作废回退 ==============================

    private static Guid SeedLinkedShipment(LinkedHarness h, int quantity, Guid? orderId, Guid? orderItemId)
    {
        var shipment = new SalesShipment
        {
            Id = Guid.NewGuid(),
            ShipmentNo = "GI202601010001",
            PartnerId = h.Partner.Id,
            PartnerName = h.Partner.Name,
            OrderDate = OrderDate,
            OrderId = orderId,
            OrderNo = orderId is null ? null : h.Order.OrderNo,
            TotalAmount = quantity * 8m,
            SettledAmount = 0m,
            Status = OrderStatus.Normal,
            CreatedAt = OrderDate,
            UpdatedAt = OrderDate,
        };
        h.Shipments.Seed(shipment, new[]
        {
            new SalesShipmentItem
            {
                Id = SequentialGuidGenerator.NewSequential(),
                ShipmentId = shipment.Id,
                ProductId = h.Product.Id,
                ProductName = h.Product.Name,
                Unit = h.Product.Unit,
                Quantity = quantity,
                UnitPrice = 8m,
                Subtotal = quantity * 8m,
                OrderItemId = orderItemId,
            },
        });
        h.Inventory.Seed(h.Product.Id, 0);
        return shipment.Id;
    }

    private static VoidSalesShipmentRequestHandler CreateVoidHandler(LinkedHarness h)
        => new(h.Shipments, h.Orders, h.Inventory, h.Movements, GeneralLedgerStubs.NewVouchers(), GeneralLedgerStubs.NewPeriods(), h.Uow, h.User, TestSupport.AuditLogger);

    [Fact]
    public async Task 作废关联出库单_应回退累计已发并回到待发货()
    {
        var h = await CreateLinkedAsync(orderedQuantity: 10, fulfilledQuantity: 4, flowStatus: OrderFlowStatus.Partial);
        var shipmentId = SeedLinkedShipment(h, quantity: 4, orderId: h.Order.Id, orderItemId: h.OrderItem.Id);

        await CreateVoidHandler(h).HandleAsync(new VoidSalesShipmentRequest { Id = shipmentId });

        Assert.Equal(new[] { (h.OrderItem.Id, -4) }, h.Orders.FulfilledAdds.ToArray());
        Assert.Equal(new[] { OrderFlowStatus.Pending }, h.Orders.FlowStatusUpdates.ToArray());
        Assert.Equal(0, h.Orders.ItemsOf(h.Order.Id)[0].FulfilledQuantity);
        Assert.Equal(4, h.Inventory.GetQuantity(h.Product.Id)); // 库存回增
    }

    [Fact]
    public async Task 作废关联出库单_订单已关闭_应回退累计量但不改订单状态()
    {
        var h = await CreateLinkedAsync(orderedQuantity: 10, fulfilledQuantity: 4, flowStatus: OrderFlowStatus.Closed);
        var shipmentId = SeedLinkedShipment(h, quantity: 4, orderId: h.Order.Id, orderItemId: h.OrderItem.Id);

        await CreateVoidHandler(h).HandleAsync(new VoidSalesShipmentRequest { Id = shipmentId });

        // 累计量照常回退，但「已关闭」是人工决策 → 不回退状态
        Assert.Equal(new[] { (h.OrderItem.Id, -4) }, h.Orders.FulfilledAdds.ToArray());
        Assert.Empty(h.Orders.FlowStatusUpdates);
    }
}
