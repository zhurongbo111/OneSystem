using App.Core;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.PurchaseReceipts.CreatePurchaseReceipt;
using App.Core.Features.PurchaseReceipts.VoidPurchaseReceipt;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 采购入库单关联订单测试（specs/024-erp-order-flow design.md §3.4 / §6）：
/// 关联成功（累计已收回写 + 订单状态三态）；超量 40115 / 状态不允许 40116 / 供应商不一致 40117 /
/// 明细不属于订单 40400 / 订单已作废 40104；失败路径不退不写；
/// 不关联订单时不调用订单仓储；作废回退累计量并重算状态（订单已关闭不回退状态）。
/// </summary>
public class PurchaseReceiptOrderLinkTests
{
    private static readonly DateTimeOffset OrderDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class LinkedHarness
    {
        public required AppDbContext Context { get; init; }
        public required StubCurrentUser User { get; init; }
        public required FakePurchaseReceiptRepository Receipts { get; init; }
        public required FakePurchaseOrderRepository Orders { get; init; }
        public required FakeInventoryRepository Inventory { get; init; }
        public required FakeStockMovementRepository Movements { get; init; }
        public required RecordingUnitOfWork Uow { get; init; }
        public required CreatePurchaseReceiptRequestHandler Handler { get; init; }
        public required List<string> Calls { get; init; }
        public required Partner Partner { get; init; }
        public required Product Product { get; init; }
        public required PurchaseOrder Order { get; init; }
        public required PurchaseOrderItem OrderItem { get; init; }
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
        var receipts = new FakePurchaseReceiptRepository(calls);
        var orders = new FakePurchaseOrderRepository(calls);
        var inventory = new FakeInventoryRepository(calls);
        var movements = new FakeStockMovementRepository(calls);
        var uow = new RecordingUnitOfWork(calls);

        var partner = TestSupport.NewPartner("联动供应商");
        var product = TestSupport.NewProduct("sku-link", "联动商品", 5m);
        context.Partners.Add(partner);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var order = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            OrderNo = "PO202601010001",
            PartnerId = orderPartnerId ?? partner.Id,
            PartnerName = partner.Name,
            OrderDate = OrderDate,
            TotalAmount = orderedQuantity * 5m,
            FlowStatus = flowStatus,
            CreatedAt = OrderDate,
            UpdatedAt = OrderDate,
        };
        var orderItem = new PurchaseOrderItem
        {
            Id = SequentialGuidGenerator.NewSequential(),
            OrderId = order.Id,
            ProductId = product.Id,
            ProductName = product.Name,
            Unit = product.Unit,
            Quantity = orderedQuantity,
            UnitPrice = 5m,
            Subtotal = orderedQuantity * 5m,
            FulfilledQuantity = fulfilledQuantity,
        };
        orders.Seed(order, new[] { orderItem });

        var gl = GeneralLedgerStubs.Create();
        var handler = new CreatePurchaseReceiptRequestHandler(
            receipts, orders, new PartnerRepository(context), new ProductRepository(context), new FakeWarehouseRepository(),
            inventory, movements, gl.Vouchers, gl.Mappings, gl.Periods, gl.Accounts, uow, user, TestSupport.AuditLogger);

        return new LinkedHarness
        {
            Context = context,
            User = user,
            Receipts = receipts,
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

    private static CreatePurchaseReceiptRequest LinkedRequest(LinkedHarness h, int quantity, Guid? orderItemId = null)
        => new()
        {
            PartnerId = h.Partner.Id,
            OrderDate = OrderDate,
            OrderId = h.Order.Id,
            Items = new[]
            {
                new CreatePurchaseReceiptItem
                {
                    ProductId = h.Product.Id,
                    Quantity = quantity,
                    UnitPrice = 5m,
                    OrderItemId = orderItemId ?? h.OrderItem.Id,
                },
            },
        };

    // ============================== 关联成功 ==============================

    [Fact]
    public async Task 关联订单_部分收货_应回写累计已收并置部分收货()
    {
        var h = await CreateLinkedAsync(orderedQuantity: 10);

        var result = await h.Handler.HandleAsync(LinkedRequest(h, 4));

        // 入库单带订单号快照
        Assert.Equal(h.Order.Id.ToString(), result.OrderId);
        Assert.Equal(h.Order.OrderNo, result.OrderNo);
        Assert.Equal(h.OrderItem.Id.ToString(), result.Items[0].OrderItemId);

        // 回写累计已收 + 订单状态推导
        Assert.Equal(new[] { (h.OrderItem.Id, 4) }, h.Orders.FulfilledAdds.ToArray());
        Assert.Equal(new[] { OrderFlowStatus.Partial }, h.Orders.FlowStatusUpdates.ToArray());
        Assert.Equal(4, h.Orders.ItemsOf(h.Order.Id)[0].FulfilledQuantity);
    }

    [Fact]
    public async Task 关联订单_收满_应置已完成()
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

        await h.Handler.HandleAsync(new CreatePurchaseReceiptRequest
        {
            PartnerId = h.Partner.Id,
            OrderDate = OrderDate,
            Items = new[] { new CreatePurchaseReceiptItem { ProductId = h.Product.Id, Quantity = 2, UnitPrice = 5m } },
        });

        Assert.Empty(h.Orders.FulfilledAdds);
        Assert.Empty(h.Orders.FlowStatusUpdates);
        Assert.Equal(2, h.Inventory.GetQuantity(h.Product.Id));
    }

    // ============================== 关联异常分支 ==============================

    [Fact]
    public async Task 关联订单_超出未收数量_应报OrderFulfillExceeded且不落单()
    {
        var h = await CreateLinkedAsync(orderedQuantity: 10, fulfilledQuantity: 8);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => h.Handler.HandleAsync(LinkedRequest(h, 3)));

        Assert.Equal(ErrorCode.OrderFulfillExceeded, ex.Code);
        Assert.Contains("PO202601010001", ex.Message);
        Assert.Contains("未收 2", ex.Message);

        // 不退不写：无库存变化 / 无流水 / 无累计量变化 / 未开启事务
        Assert.Empty(h.Inventory.Increments);
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
        Assert.Empty(h.Inventory.Increments);
    }

    [Fact]
    public async Task 关联订单_订单已作废_应报OrderVoided()
    {
        var h = await CreateLinkedAsync(flowStatus: OrderFlowStatus.Voided);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => h.Handler.HandleAsync(LinkedRequest(h, 1)));

        Assert.Equal(ErrorCode.OrderVoided, ex.Code);
    }

    [Fact]
    public async Task 关联订单_供应商不一致_应报OrderPartnerMismatch()
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
        Assert.Empty(h.Inventory.Increments);
    }

    [Fact]
    public async Task 关联订单_订单不存在_应报NotFound()
    {
        var h = await CreateLinkedAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => h.Handler.HandleAsync(new CreatePurchaseReceiptRequest
        {
            PartnerId = h.Partner.Id,
            OrderDate = OrderDate,
            OrderId = Guid.NewGuid(),
            Items = new[] { new CreatePurchaseReceiptItem { ProductId = h.Product.Id, Quantity = 1, UnitPrice = 5m, OrderItemId = Guid.NewGuid() } },
        }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 作废回退 ==============================

    private static void SeedLinkedReceipt(LinkedHarness h, int quantity, Guid? orderId, Guid? orderItemId)
    {
        var receipt = new PurchaseReceipt
        {
            Id = Guid.NewGuid(),
            ReceiptNo = "GR202601010001",
            PartnerId = h.Partner.Id,
            PartnerName = h.Partner.Name,
            OrderDate = OrderDate,
            OrderId = orderId,
            OrderNo = orderId is null ? null : h.Order.OrderNo,
            TotalAmount = quantity * 5m,
            SettledAmount = 0m,
            Status = OrderStatus.Normal,
            CreatedAt = OrderDate,
            UpdatedAt = OrderDate,
        };
        h.Receipts.Seed(receipt, new[]
        {
            new PurchaseReceiptItem
            {
                Id = SequentialGuidGenerator.NewSequential(),
                ReceiptId = receipt.Id,
                ProductId = h.Product.Id,
                ProductName = h.Product.Name,
                Unit = h.Product.Unit,
                Quantity = quantity,
                UnitPrice = 5m,
                Subtotal = quantity * 5m,
                OrderItemId = orderItemId,
            },
        });
        h.Inventory.Seed(h.Product.Id, quantity);
    }

    private static VoidPurchaseReceiptRequestHandler CreateVoidHandler(LinkedHarness h)
        => new(h.Receipts, h.Orders, h.Inventory, h.Movements, GeneralLedgerStubs.NewVouchers(), GeneralLedgerStubs.NewPeriods(), h.Uow, h.User, TestSupport.AuditLogger);

    [Fact]
    public async Task 作废关联入库单_应回退累计已收并回到待收货()
    {
        var h = await CreateLinkedAsync(orderedQuantity: 10, fulfilledQuantity: 4, flowStatus: OrderFlowStatus.Partial);
        SeedLinkedReceipt(h, quantity: 4, orderId: h.Order.Id, orderItemId: h.OrderItem.Id);

        var receiptId = h.Receipts.Orders.First().Id;
        await CreateVoidHandler(h).HandleAsync(new VoidPurchaseReceiptRequest { Id = receiptId });

        Assert.Equal(new[] { (h.OrderItem.Id, -4) }, h.Orders.FulfilledAdds.ToArray());
        Assert.Equal(new[] { OrderFlowStatus.Pending }, h.Orders.FlowStatusUpdates.ToArray());
        Assert.Equal(0, h.Orders.ItemsOf(h.Order.Id)[0].FulfilledQuantity);
    }

    [Fact]
    public async Task 作废关联入库单_订单已关闭_应回退累计量但不改订单状态()
    {
        var h = await CreateLinkedAsync(orderedQuantity: 10, fulfilledQuantity: 4, flowStatus: OrderFlowStatus.Closed);
        SeedLinkedReceipt(h, quantity: 4, orderId: h.Order.Id, orderItemId: h.OrderItem.Id);

        var receiptId = h.Receipts.Orders.First().Id;
        await CreateVoidHandler(h).HandleAsync(new VoidPurchaseReceiptRequest { Id = receiptId });

        // 累计量照常回退，但「已关闭」是人工决策 → 不回退状态
        Assert.Equal(new[] { (h.OrderItem.Id, -4) }, h.Orders.FulfilledAdds.ToArray());
        Assert.Empty(h.Orders.FlowStatusUpdates);
    }

    [Fact]
    public async Task 作废未关联入库单_不应调用订单仓储()
    {
        var h = await CreateLinkedAsync();
        SeedLinkedReceipt(h, quantity: 3, orderId: null, orderItemId: null);

        var receiptId = h.Receipts.Orders.First().Id;
        await CreateVoidHandler(h).HandleAsync(new VoidPurchaseReceiptRequest { Id = receiptId });

        Assert.Empty(h.Orders.FulfilledAdds);
        Assert.Empty(h.Orders.FlowStatusUpdates);
        Assert.Equal(0, h.Inventory.GetQuantity(h.Product.Id));
    }
}
