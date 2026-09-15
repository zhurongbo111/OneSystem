using App.Core;
using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Purchases;
using App.Core.Features.Purchases.GetPurchaseOrderById;
using App.Core.Features.Purchases.UpdatePurchaseOrderSettlement;
using App.Core.Features.Purchases.VoidPurchaseOrder;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 采购单生命周期用例测试（design.md §6）：
/// VoidPurchaseOrder（成功逐行回冲 IncrementAsync(-qty) + UpdateStatusAsync(Voided)；不存在 40400；已作废 40104 且不重复回冲）；
/// UpdatePurchaseOrderSettlement（成功切换；已作废 40104；不存在 40400；幂等）；
/// GetPurchaseOrderById（存在含明细映射；不存在 40400）。
/// 采购单 / 库存 / 工作单元用行为型假实现（规避 InMemory 不支持 ExecuteUpdateAsync）。
/// </summary>
public class PurchaseOrderLifecycleTests
{
    private static readonly DateTimeOffset OrderDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 构造一张正常采购单（2 行明细：3×1.5 + 2×10 = 24.5）+ 商品 + 库存（10 / 8），预置进假仓储。
    /// </summary>
    private static (FakePurchaseOrderRepository Orders, FakeInventoryRepository Inventory,
        RecordingUnitOfWork Uow, StubCurrentUser User, PurchaseOrder Order, Product P1, Product P2, List<string> Calls) SeedNormal()
    {
        var calls = new List<string>();
        var orders = new FakePurchaseOrderRepository(calls);
        var inventory = new FakeInventoryRepository(calls);
        var uow = new RecordingUnitOfWork(calls);
        var user = new StubCurrentUser(Guid.NewGuid());

        var partner = TestSupport.NewPartner("供应商");
        var p1 = TestSupport.NewProduct("sku-v1", "商品一");
        var p2 = TestSupport.NewProduct("sku-v2", "商品二");
        inventory.Seed(p1.Id, 10);
        inventory.Seed(p2.Id, 8);

        var now = DateTimeOffset.UtcNow;
        var order = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            OrderNo = "PO202601010001",
            PartnerId = partner.Id,
            PartnerName = partner.Name,
            OrderDate = OrderDate,
            TotalAmount = 24.5m,
            SettlementStatus = OrderSettlementStatus.Unsettled,
            Status = OrderStatus.Normal,
            CreatedAt = now,
            UpdatedAt = now,
        };
        // 明细 Id 用顺序 Guid（与生产一致），保证按 Id 排序还原插入顺序
        var items = new List<PurchaseOrderItem>
        {
            new() { Id = SequentialGuidGenerator.NewSequential(), OrderId = order.Id, ProductId = p1.Id, ProductName = p1.Name, Unit = p1.Unit, Quantity = 3, UnitPrice = 1.5m, Subtotal = 4.5m },
            new() { Id = SequentialGuidGenerator.NewSequential(), OrderId = order.Id, ProductId = p2.Id, ProductName = p2.Name, Unit = p2.Unit, Quantity = 2, UnitPrice = 10m, Subtotal = 20m },
        };
        orders.Seed(order, items);

        return (orders, inventory, uow, user, order, p1, p2, calls);
    }

    // ============================== VoidPurchaseOrder ==============================

    [Fact]
    public async Task 作废采购单_成功_应逐行回冲库存并置作废()
    {
        var (orders, inventory, uow, user, order, p1, p2, calls) = SeedNormal();
        var handler = new VoidPurchaseOrderRequestHandler(orders, inventory, uow, user);

        var result = await handler.HandleAsync(new VoidPurchaseOrderRequest { Id = order.Id });

        // 回冲：逐行库存 -= 数量，调用顺序与明细一致
        Assert.Equal(
            new[] { (p1.Id, -3), (p2.Id, -2) },
            inventory.Increments.Select(i => (i.ProductId, i.Delta)).ToArray());
        Assert.Equal(7, inventory.GetQuantity(p1.Id)); // 10 - 3
        Assert.Equal(6, inventory.GetQuantity(p2.Id)); // 8 - 2

        // 状态置作废（事务序列：Begin → Increment ×2 → UpdateStatus → Commit）
        var afterVoid = await orders.GetDetailAsync(order.Id);
        Assert.Equal(OrderStatus.Voided, afterVoid!.Status);
        Assert.Equal(user.UserId, order.UpdatedBy);
        Assert.Equal((int)OrderStatus.Voided, result.Status);
        Assert.Equal(new[] { "Begin", "Increment", "Increment", "UpdateStatus", "Commit" }, calls.ToArray());
    }

    [Fact]
    public async Task 作废采购单_不存在_应报NotFound()
    {
        var (orders, inventory, uow, user, _, _, _, _) = SeedNormal();
        var handler = new VoidPurchaseOrderRequestHandler(orders, inventory, uow, user);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new VoidPurchaseOrderRequest { Id = Guid.NewGuid() }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 作废采购单_已作废_应报OrderVoided且不重复回冲()
    {
        var (orders, inventory, uow, user, order, p1, _, calls) = SeedNormal();
        // 预置为已作废
        await orders.UpdateStatusAsync(order.Id, OrderStatus.Voided, null);

        var handler = new VoidPurchaseOrderRequestHandler(orders, inventory, uow, user);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new VoidPurchaseOrderRequest { Id = order.Id }));
        Assert.Equal(ErrorCode.OrderVoided, ex.Code);

        // 未开启事务、未回冲
        Assert.Empty(inventory.Increments);
        Assert.DoesNotContain("Begin", calls);
        Assert.Equal(10, inventory.GetQuantity(p1.Id));
    }

    // ============================== UpdatePurchaseOrderSettlement ==============================

    [Fact]
    public async Task 更新结算_成功_应切换结算状态()
    {
        var (orders, inventory, uow, user, order, p1, _, calls) = SeedNormal();
        var handler = new UpdatePurchaseOrderSettlementRequestHandler(orders, user);

        var result = await handler.HandleAsync(
            new UpdatePurchaseOrderSettlementRequest { Id = order.Id, SettlementStatus = (int)OrderSettlementStatus.Settled });

        Assert.Equal((int)OrderSettlementStatus.Settled, result.SettlementStatus);
        var afterSettle = await orders.GetDetailAsync(order.Id);
        Assert.Equal(OrderSettlementStatus.Settled, afterSettle!.SettlementStatus);
        Assert.Equal(user.UserId, order.UpdatedBy);
        Assert.Contains("UpdateSettlement", calls);
        // 库存不受结算影响
        Assert.Equal(10, inventory.GetQuantity(p1.Id));
        Assert.Empty(inventory.Increments);
    }

    [Fact]
    public async Task 更新结算_已作废_应报OrderVoided()
    {
        var (orders, inventory, uow, user, order, _, _, _) = SeedNormal();
        await orders.UpdateStatusAsync(order.Id, OrderStatus.Voided, null);

        var handler = new UpdatePurchaseOrderSettlementRequestHandler(orders, user);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            new UpdatePurchaseOrderSettlementRequest { Id = order.Id, SettlementStatus = (int)OrderSettlementStatus.Settled }));
        Assert.Equal(ErrorCode.OrderVoided, ex.Code);
    }

    [Fact]
    public async Task 更新结算_不存在_应报NotFound()
    {
        var (orders, inventory, uow, user, _, _, _, _) = SeedNormal();
        var handler = new UpdatePurchaseOrderSettlementRequestHandler(orders, user);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            new UpdatePurchaseOrderSettlementRequest { Id = Guid.NewGuid(), SettlementStatus = (int)OrderSettlementStatus.Settled }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 更新结算_目标与当前相同_应为无操作()
    {
        var (orders, _, uow, user, order, _, _, calls) = SeedNormal();
        var handler = new UpdatePurchaseOrderSettlementRequestHandler(orders, user);

        // 当前为 Unsettled，再设为 Unsettled → 幂等，不调用 UpdateSettlementAsync
        var result = await handler.HandleAsync(
            new UpdatePurchaseOrderSettlementRequest { Id = order.Id, SettlementStatus = (int)OrderSettlementStatus.Unsettled });
        Assert.Equal((int)OrderSettlementStatus.Unsettled, result.SettlementStatus);
        Assert.DoesNotContain("UpdateSettlement", calls);
    }

    // ============================== GetPurchaseOrderById ==============================

    [Fact]
    public async Task 查询采购单详情_存在_应返回主表与明细()
    {
        var (orders, _, uow, _, order, _, _, _) = SeedNormal();
        var handler = new GetPurchaseOrderByIdRequestHandler(orders);

        var result = await handler.HandleAsync(new GetPurchaseOrderByIdRequest { Id = order.Id });

        Assert.Equal(order.OrderNo, result.OrderNo);
        Assert.Equal(24.5m, result.TotalAmount);
        Assert.Equal(2, result.Items.Count);
        // 明细按插入顺序、快照字段原样返回
        Assert.Equal("商品一", result.Items[0].ProductName);
        Assert.Equal(4.5m, result.Items[0].Subtotal);
        Assert.Equal("商品二", result.Items[1].ProductName);
        Assert.Equal(20m, result.Items[1].Subtotal);
    }

    [Fact]
    public async Task 查询采购单详情_不存在_应报NotFound()
    {
        var (orders, _, uow, _, _, _, _, _) = SeedNormal();
        var handler = new GetPurchaseOrderByIdRequestHandler(orders);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new GetPurchaseOrderByIdRequest { Id = Guid.NewGuid() }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }
}
