using App.Core;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.SalesShipments.GetSalesShipmentById;
using App.Core.Features.SalesShipments.VoidSalesShipment;

namespace App.Tests;

/// <summary>
/// 销售单生命周期用例测试（design.md §6）：
/// VoidSalesShipment（成功逐行回冲 IncrementAsync(+qty) + UpdateStatusAsync(Voided)；不存在 40400；已作废 40104 且不重复回冲）；
/// GetSalesShipmentById（存在含明细映射；不存在 40400）；
/// 结算状态推导（specs/023-erp-settlement §0：SettledAmount ≤ 0 / 部分 / ≥ 总额 三态）。
/// 销售单 / 库存 / 工作单元用行为型假实现（规避 InMemory 不支持 ExecuteUpdateAsync）。
/// </summary>
public class SalesShipmentLifecycleTests
{
    private static readonly DateTimeOffset OrderDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 构造一张正常销售单（2 行明细：3×1.5 + 2×10 = 24.5）+ 商品 + 库存（10 / 8），预置进假仓储。
    /// </summary>
    private static (FakeSalesShipmentRepository Orders, FakeInventoryRepository Inventory,
        RecordingUnitOfWork Uow, StubCurrentUser User, SalesShipment Order, Product P1, Product P2, List<string> Calls) SeedNormal()
    {
        var calls = new List<string>();
        var orders = new FakeSalesShipmentRepository(calls);
        var inventory = new FakeInventoryRepository(calls);
        var uow = new RecordingUnitOfWork(calls);
        var user = new StubCurrentUser(Guid.NewGuid());

        var partner = TestSupport.NewPartner("客户", type: PartnerType.Customer);
        var p1 = TestSupport.NewProduct("sku-s1", "商品一");
        var p2 = TestSupport.NewProduct("sku-s2", "商品二");
        inventory.Seed(p1.Id, 10);
        inventory.Seed(p2.Id, 8);

        var now = DateTimeOffset.UtcNow;
        var order = new SalesShipment
        {
            Id = Guid.NewGuid(),
            ShipmentNo = "GI202601010001",
            PartnerId = partner.Id,
            PartnerName = partner.Name,
            OrderDate = OrderDate,
            TotalAmount = 24.5m,
            SettledAmount = 0m,
            Status = OrderStatus.Normal,
            CreatedAt = now,
            UpdatedAt = now,
        };
        // 明细 Id 用顺序 Guid（与生产一致），保证按 Id 排序还原插入顺序
        var items = new List<SalesShipmentItem>
        {
            new() { Id = SequentialGuidGenerator.NewSequential(), ShipmentId = order.Id, ProductId = p1.Id, ProductName = p1.Name, Unit = p1.Unit, Quantity = 3, UnitPrice = 1.5m, Subtotal = 4.5m },
            new() { Id = SequentialGuidGenerator.NewSequential(), ShipmentId = order.Id, ProductId = p2.Id, ProductName = p2.Name, Unit = p2.Unit, Quantity = 2, UnitPrice = 10m, Subtotal = 20m },
        };
        orders.Seed(order, items);

        return (orders, inventory, uow, user, order, p1, p2, calls);
    }

    // ============================== VoidSalesShipment ==============================

    [Fact]
    public async Task 作废销售单_成功_应逐行回冲库存并置作废()
    {
        var (orders, inventory, uow, user, order, p1, p2, calls) = SeedNormal();
        var movements = new FakeStockMovementRepository(calls);
        var handler = new VoidSalesShipmentRequestHandler(orders, new FakeSalesOrderRepository(calls), inventory, movements, uow, user, TestSupport.AuditLogger);

        var result = await handler.HandleAsync(new VoidSalesShipmentRequest { Id = order.Id });

        // 回冲：逐行库存 += 数量（销售扣减的逆操作），调用顺序与明细一致
        Assert.Equal(
            new[] { (p1.Id, 3), (p2.Id, 2) },
            inventory.Increments.Select(i => (i.ProductId, i.Delta)).ToArray());
        Assert.Equal(13, inventory.GetQuantity(p1.Id)); // 10 + 3
        Assert.Equal(10, inventory.GetQuantity(p2.Id)); // 8 + 2

        // 逐行追加销售作废回增流水：类型 / 正方向 / 来源 / 操作人
        Assert.Equal(2, movements.Appended.Count);
        Assert.Equal(new[] { (p1.Id, 3), (p2.Id, 2) }, movements.Appended.Select(m => (m.ProductId, m.Quantity)).ToArray());
        Assert.All(movements.Appended, m =>
        {
            Assert.Equal(StockMovementType.SalesVoid, m.MovementType);
            Assert.Equal(user.UserId, m.CreatedBy);
            Assert.Equal(order.Id, m.SourceId);
            Assert.Equal(order.ShipmentNo, m.SourceNo);
        });

        // 状态置作废（事务序列：Begin → Increment → Append → Increment → Append → UpdateStatus → Commit）
        var (afterVoid, _) = await orders.GetDetailAsync(order.Id);
        Assert.Equal(OrderStatus.Voided, afterVoid!.Status);
        Assert.Equal(user.UserId, order.UpdatedBy);
        Assert.Equal((int)OrderStatus.Voided, result.Status);
        // 成本：销售出库作废按原出库单价回正（ApplyInboundCost），与数量回增同事务
        Assert.Equal(new[] { "Begin", "Increment", "ApplyInboundCost", "Append", "Increment", "ApplyInboundCost", "Append", "UpdateStatus", "Commit" }, calls.ToArray());
    }

    [Fact]
    public async Task 作废销售单_不存在_应报NotFound()
    {
        var (orders, inventory, uow, user, _, _, _, _) = SeedNormal();
        var handler = new VoidSalesShipmentRequestHandler(orders, new FakeSalesOrderRepository(), inventory, new FakeStockMovementRepository(), uow, user, TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new VoidSalesShipmentRequest { Id = Guid.NewGuid() }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 作废销售单_已作废_应报OrderVoided且不重复回冲()
    {
        var (orders, inventory, uow, user, order, p1, _, calls) = SeedNormal();
        // 预置为已作废
        await orders.UpdateStatusAsync(order.Id, OrderStatus.Voided, null);

        var movements = new FakeStockMovementRepository(calls);
        var handler = new VoidSalesShipmentRequestHandler(orders, new FakeSalesOrderRepository(calls), inventory, movements, uow, user, TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new VoidSalesShipmentRequest { Id = order.Id }));
        Assert.Equal(ErrorCode.OrderVoided, ex.Code);

        // 未开启事务、未回冲、不写流水
        Assert.Empty(inventory.Increments);
        Assert.Empty(movements.Appended);
        Assert.DoesNotContain("Begin", calls);
        Assert.Equal(10, inventory.GetQuantity(p1.Id));
    }

    [Fact]
    public async Task 作废销售单_已核销_应报OrderSettledCannotVoid且不回冲()
    {
        var (orders, inventory, uow, user, order, p1, _, calls) = SeedNormal();
        order.SettledAmount = 10m; // 已被收款单核销（部分）

        var movements = new FakeStockMovementRepository(calls);
        var handler = new VoidSalesShipmentRequestHandler(orders, new FakeSalesOrderRepository(calls), inventory, movements, uow, user, TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new VoidSalesShipmentRequest { Id = order.Id }));
        Assert.Equal(ErrorCode.OrderSettledCannotVoid, ex.Code);
        Assert.Contains(order.ShipmentNo, ex.Message);
        Assert.Contains("10.00", ex.Message);

        // 作废与核销互斥：未开启事务、未回冲库存、不写流水、状态保持正常
        Assert.Empty(inventory.Increments);
        Assert.Empty(movements.Appended);
        Assert.DoesNotContain("Begin", calls);
        Assert.Equal(10, inventory.GetQuantity(p1.Id));
        var (afterVoid, _) = await orders.GetDetailAsync(order.Id);
        Assert.Equal(OrderStatus.Normal, afterVoid!.Status);
    }

    // ============================== 结算状态推导（erp-settlement） ==============================

    [Theory]
    [InlineData(0, SettlementState.Unsettled)]
    [InlineData(10, SettlementState.PartiallySettled)]
    [InlineData(24.5, SettlementState.Settled)]
    [InlineData(30, SettlementState.Settled)]
    public async Task 结算状态推导_按已结金额与总额四态(decimal settledAmount, SettlementState expected)
    {
        var (orders, _, _, _, order, _, _, _) = SeedNormal();
        order.SettledAmount = settledAmount;
        var handler = new GetSalesShipmentByIdRequestHandler(orders);

        var result = await handler.HandleAsync(new GetSalesShipmentByIdRequest { Id = order.Id });

        // 0 未结算 / 1 部分结算 / 2 已结算（SettledAmount ≥ TotalAmount 视为结清）；未结金额 = 总额 − 已结
        Assert.Equal((int)expected, result.SettlementState);
        Assert.Equal(order.TotalAmount - settledAmount, result.UnsettledAmount);
        Assert.Equal(settledAmount, result.SettledAmount);
    }

    // ============================== GetSalesShipmentById ==============================

    [Fact]
    public async Task 查询销售单详情_存在_应返回主表与明细()
    {
        var (orders, _, uow, _, order, _, _, _) = SeedNormal();
        var handler = new GetSalesShipmentByIdRequestHandler(orders);

        var result = await handler.HandleAsync(new GetSalesShipmentByIdRequest { Id = order.Id });

        Assert.Equal(order.ShipmentNo, result.ShipmentNo);
        Assert.Equal(24.5m, result.TotalAmount);
        Assert.Equal(2, result.Items.Count);
        // 明细按插入顺序、快照字段原样返回
        Assert.Equal("商品一", result.Items[0].ProductName);
        Assert.Equal(4.5m, result.Items[0].Subtotal);
        Assert.Equal("商品二", result.Items[1].ProductName);
        Assert.Equal(20m, result.Items[1].Subtotal);
    }

    [Fact]
    public async Task 查询销售单详情_不存在_应报NotFound()
    {
        var (orders, _, uow, _, _, _, _, _) = SeedNormal();
        var handler = new GetSalesShipmentByIdRequestHandler(orders);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new GetSalesShipmentByIdRequest { Id = Guid.NewGuid() }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }
}
