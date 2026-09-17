using App.Core;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.PurchaseReceipts.GetPurchaseReceiptById;
using App.Core.Features.PurchaseReceipts.VoidPurchaseReceipt;

namespace App.Tests;

/// <summary>
/// 采购单生命周期用例测试（design.md §6）：
/// VoidPurchaseReceipt（成功逐行回冲 IncrementAsync(-qty) + UpdateStatusAsync(Voided)；不存在 40400；已作废 40104 且不重复回冲）；
/// GetPurchaseReceiptById（存在含明细映射；不存在 40400）；
/// 结算状态推导（specs/023-erp-settlement §0：SettledAmount ≤ 0 / 部分 / ≥ 总额 三态）。
/// 采购单 / 库存 / 工作单元用行为型假实现（规避 InMemory 不支持 ExecuteUpdateAsync）。
/// </summary>
public class PurchaseReceiptLifecycleTests
{
    private static readonly DateTimeOffset OrderDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 构造一张正常采购单（2 行明细：3×1.5 + 2×10 = 24.5）+ 商品 + 库存（10 / 8），预置进假仓储。
    /// </summary>
    private static (FakePurchaseReceiptRepository Orders, FakeInventoryRepository Inventory,
        RecordingUnitOfWork Uow, StubCurrentUser User, PurchaseReceipt Order, Product P1, Product P2, List<string> Calls) SeedNormal()
    {
        var calls = new List<string>();
        var orders = new FakePurchaseReceiptRepository(calls);
        var inventory = new FakeInventoryRepository(calls);
        var uow = new RecordingUnitOfWork(calls);
        var user = new StubCurrentUser(Guid.NewGuid());

        var partner = TestSupport.NewPartner("供应商");
        var p1 = TestSupport.NewProduct("sku-v1", "商品一");
        var p2 = TestSupport.NewProduct("sku-v2", "商品二");
        inventory.Seed(p1.Id, 10);
        inventory.Seed(p2.Id, 8);

        var now = DateTimeOffset.UtcNow;
        var order = new PurchaseReceipt
        {
            Id = Guid.NewGuid(),
            ReceiptNo = "GR202601010001",
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
        var items = new List<PurchaseReceiptItem>
        {
            new() { Id = SequentialGuidGenerator.NewSequential(), ReceiptId = order.Id, ProductId = p1.Id, ProductName = p1.Name, Unit = p1.Unit, Quantity = 3, UnitPrice = 1.5m, Subtotal = 4.5m },
            new() { Id = SequentialGuidGenerator.NewSequential(), ReceiptId = order.Id, ProductId = p2.Id, ProductName = p2.Name, Unit = p2.Unit, Quantity = 2, UnitPrice = 10m, Subtotal = 20m },
        };
        orders.Seed(order, items);

        return (orders, inventory, uow, user, order, p1, p2, calls);
    }

    // ============================== VoidPurchaseReceipt ==============================

    [Fact]
    public async Task 作废采购单_成功_应逐行回冲库存并置作废()
    {
        var (orders, inventory, uow, user, order, p1, p2, calls) = SeedNormal();
        var movements = new FakeStockMovementRepository(calls);
        var handler = new VoidPurchaseReceiptRequestHandler(orders, new FakePurchaseOrderRepository(calls), inventory, movements, uow, user);

        var result = await handler.HandleAsync(new VoidPurchaseReceiptRequest { Id = order.Id });

        // 回冲：逐行库存 -= 数量，调用顺序与明细一致
        Assert.Equal(
            new[] { (p1.Id, -3), (p2.Id, -2) },
            inventory.Increments.Select(i => (i.ProductId, i.Delta)).ToArray());
        Assert.Equal(7, inventory.GetQuantity(p1.Id)); // 10 - 3
        Assert.Equal(6, inventory.GetQuantity(p2.Id)); // 8 - 2

        // 逐行追加采购作废回冲流水：类型 / 负方向 / 来源 / 操作人
        Assert.Equal(2, movements.Appended.Count);
        Assert.Equal(new[] { (p1.Id, -3), (p2.Id, -2) }, movements.Appended.Select(m => (m.ProductId, m.Quantity)).ToArray());
        Assert.All(movements.Appended, m =>
        {
            Assert.Equal(StockMovementType.PurchaseVoid, m.MovementType);
            Assert.Equal(user.UserId, m.CreatedBy);
            Assert.Equal(order.Id, m.SourceId);
            Assert.Equal(order.ReceiptNo, m.SourceNo);
        });
        // 状态置作废（事务序列：Begin → Increment ×2 → Append ×2 → UpdateStatus → Commit）
        var (afterVoid, _) = await orders.GetDetailAsync(order.Id);
        Assert.Equal(OrderStatus.Voided, afterVoid!.Status);
        Assert.Equal(user.UserId, order.UpdatedBy);
        Assert.Equal((int)OrderStatus.Voided, result.Status);
        Assert.Equal(new[] { "Begin", "Increment", "Append", "Increment", "Append", "UpdateStatus", "Commit" }, calls.ToArray());
    }

    [Fact]
    public async Task 作废采购单_不存在_应报NotFound()
    {
        var (orders, inventory, uow, user, _, _, _, _) = SeedNormal();
        var handler = new VoidPurchaseReceiptRequestHandler(orders, new FakePurchaseOrderRepository(), inventory, new FakeStockMovementRepository(), uow, user);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new VoidPurchaseReceiptRequest { Id = Guid.NewGuid() }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 作废采购单_已作废_应报OrderVoided且不重复回冲()
    {
        var (orders, inventory, uow, user, order, p1, _, calls) = SeedNormal();
        // 预置为已作废
        await orders.UpdateStatusAsync(order.Id, OrderStatus.Voided, null);

        var movements = new FakeStockMovementRepository(calls);
        var handler = new VoidPurchaseReceiptRequestHandler(orders, new FakePurchaseOrderRepository(calls), inventory, movements, uow, user);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new VoidPurchaseReceiptRequest { Id = order.Id }));
        Assert.Equal(ErrorCode.OrderVoided, ex.Code);

        // 未开启事务、未回冲、不写流水
        Assert.Empty(inventory.Increments);
        Assert.Empty(movements.Appended);
        Assert.DoesNotContain("Begin", calls);
        Assert.Equal(10, inventory.GetQuantity(p1.Id));
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
        var handler = new GetPurchaseReceiptByIdRequestHandler(orders);

        var result = await handler.HandleAsync(new GetPurchaseReceiptByIdRequest { Id = order.Id });

        // 0 未结算 / 1 部分结算 / 2 已结算（SettledAmount ≥ TotalAmount 视为结清）；未结金额 = 总额 − 已结
        Assert.Equal((int)expected, result.SettlementState);
        Assert.Equal(order.TotalAmount - settledAmount, result.UnsettledAmount);
        Assert.Equal(settledAmount, result.SettledAmount);
    }

    // ============================== GetPurchaseReceiptById ==============================

    [Fact]
    public async Task 查询采购单详情_存在_应返回主表与明细()
    {
        var (orders, _, uow, _, order, _, _, _) = SeedNormal();
        var handler = new GetPurchaseReceiptByIdRequestHandler(orders);

        var result = await handler.HandleAsync(new GetPurchaseReceiptByIdRequest { Id = order.Id });

        Assert.Equal(order.ReceiptNo, result.ReceiptNo);
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
        var handler = new GetPurchaseReceiptByIdRequestHandler(orders);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new GetPurchaseReceiptByIdRequest { Id = Guid.NewGuid() }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }
}
