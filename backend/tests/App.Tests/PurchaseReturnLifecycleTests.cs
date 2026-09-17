using App.Core;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.PurchaseReturns.CreatePurchaseReturn;
using App.Core.Features.PurchaseReturns.GetPurchaseReturnById;
using App.Core.Features.PurchaseReturns.GetPurchaseReturns;
using App.Core.Features.PurchaseReturns.UpdatePurchaseReturnSettlement;
using App.Core.Features.PurchaseReturns.VoidPurchaseReturn;
using App.Core.Features.Purchases.CreatePurchaseOrder;
using App.Core.Responses;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 采购退货单生命周期用例测试（design.md §6）：
/// VoidPurchaseReturn（成功逐行回冲 IncrementAsync(+qty) + 正方向流水 + UpdateStatusAsync(Voided)；不存在 40400；已作废 40104 且不重复回冲）；
/// UpdatePurchaseReturnSettlement（成功切换；已作废 40104；不存在 40400；幂等）；
/// GetPurchaseReturnById（存在含明细映射；不存在 40400）；GetPurchaseReturns（筛选传参 + 分页映射）；
/// 对账一致性（采购入库 → 退货 → 退货作废链路后 Σ 流水变动量 == Inventory.Quantity）。
/// 单据 / 库存 / 流水 / 工作单元用行为型假实现（规避 InMemory 不支持 ExecuteUpdateAsync）。
/// </summary>
public class PurchaseReturnLifecycleTests
{
    private static readonly DateTimeOffset ReturnDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 构造一张正常采购退货单（2 行明细：3×1.5 + 2×10 = 24.5）+ 商品 + 库存（10 / 8），预置进假仓储。
    /// </summary>
    private static (FakePurchaseReturnRepository Returns, FakeInventoryRepository Inventory,
        RecordingUnitOfWork Uow, StubCurrentUser User, PurchaseReturn PurchaseReturn, Product P1, Product P2, List<string> Calls) SeedNormal()
    {
        var calls = new List<string>();
        var returns = new FakePurchaseReturnRepository(calls);
        var inventory = new FakeInventoryRepository(calls);
        var uow = new RecordingUnitOfWork(calls);
        var user = new StubCurrentUser(Guid.NewGuid());

        var partner = TestSupport.NewPartner("供应商");
        var p1 = TestSupport.NewProduct("sku-pr-v1", "商品一");
        var p2 = TestSupport.NewProduct("sku-pr-v2", "商品二");
        inventory.Seed(p1.Id, 10);
        inventory.Seed(p2.Id, 8);

        var now = DateTimeOffset.UtcNow;
        var purchaseReturn = new PurchaseReturn
        {
            Id = Guid.NewGuid(),
            ReturnNo = "PR202601010001",
            PartnerId = partner.Id,
            PartnerName = partner.Name,
            ReturnDate = ReturnDate,
            TotalAmount = 24.5m,
            SettlementStatus = OrderSettlementStatus.Unsettled,
            Status = OrderStatus.Normal,
            CreatedAt = now,
            UpdatedAt = now,
        };
        // 明细 Id 用顺序 Guid（与生产一致），保证按 Id 排序还原插入顺序
        var items = new List<PurchaseReturnItem>
        {
            new() { Id = SequentialGuidGenerator.NewSequential(), ReturnId = purchaseReturn.Id, ProductId = p1.Id, ProductName = p1.Name, Unit = p1.Unit, Quantity = 3, UnitPrice = 1.5m, Subtotal = 4.5m },
            new() { Id = SequentialGuidGenerator.NewSequential(), ReturnId = purchaseReturn.Id, ProductId = p2.Id, ProductName = p2.Name, Unit = p2.Unit, Quantity = 2, UnitPrice = 10m, Subtotal = 20m },
        };
        returns.Seed(purchaseReturn, items);

        return (returns, inventory, uow, user, purchaseReturn, p1, p2, calls);
    }

    // ============================== VoidPurchaseReturn ==============================

    [Fact]
    public async Task 作废采购退货单_成功_应逐行回冲库存并置作废()
    {
        var (returns, inventory, uow, user, purchaseReturn, p1, p2, calls) = SeedNormal();
        var movements = new FakeStockMovementRepository(calls);
        var handler = new VoidPurchaseReturnRequestHandler(returns, inventory, movements, uow, user);

        var result = await handler.HandleAsync(new VoidPurchaseReturnRequest { Id = purchaseReturn.Id });

        // 回冲：逐行库存 += 退货数量（把退回供应商的实物收回账上），调用顺序与明细一致
        Assert.Equal(
            new[] { (p1.Id, 3), (p2.Id, 2) },
            inventory.Increments.Select(i => (i.ProductId, i.Delta)).ToArray());
        Assert.Equal(13, inventory.GetQuantity(p1.Id)); // 10 + 3
        Assert.Equal(10, inventory.GetQuantity(p2.Id)); // 8 + 2

        // 逐行追加采购退货作废流水：类型 / 正方向 / 来源 / 操作人
        Assert.Equal(2, movements.Appended.Count);
        Assert.Equal(new[] { (p1.Id, 3), (p2.Id, 2) }, movements.Appended.Select(m => (m.ProductId, m.Quantity)).ToArray());
        Assert.All(movements.Appended, m =>
        {
            Assert.Equal(StockMovementType.PurchaseReturnVoid, m.MovementType);
            Assert.Equal(user.UserId, m.CreatedBy);
            Assert.Equal(purchaseReturn.Id, m.SourceId);
            Assert.Equal(purchaseReturn.ReturnNo, m.SourceNo);
        });
        // 状态置作废（事务序列：Begin → Increment → Append ×2 交替 → UpdateStatus → Commit）
        var (afterVoid, _) = await returns.GetDetailAsync(purchaseReturn.Id);
        Assert.Equal(OrderStatus.Voided, afterVoid!.Status);
        Assert.Equal(user.UserId, purchaseReturn.UpdatedBy);
        Assert.Equal((int)OrderStatus.Voided, result.Status);
        Assert.Equal(new[] { "Begin", "Increment", "Append", "Increment", "Append", "UpdateStatus", "Commit" }, calls.ToArray());
    }

    [Fact]
    public async Task 作废采购退货单_不存在_应报NotFound()
    {
        var (returns, inventory, uow, user, _, _, _, _) = SeedNormal();
        var handler = new VoidPurchaseReturnRequestHandler(returns, inventory, new FakeStockMovementRepository(), uow, user);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new VoidPurchaseReturnRequest { Id = Guid.NewGuid() }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 作废采购退货单_已作废_应报OrderVoided且不重复回冲()
    {
        var (returns, inventory, uow, user, purchaseReturn, p1, _, calls) = SeedNormal();
        // 预置为已作废
        await returns.UpdateStatusAsync(purchaseReturn.Id, OrderStatus.Voided, null);

        var movements = new FakeStockMovementRepository(calls);
        var handler = new VoidPurchaseReturnRequestHandler(returns, inventory, movements, uow, user);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new VoidPurchaseReturnRequest { Id = purchaseReturn.Id }));
        Assert.Equal(ErrorCode.OrderVoided, ex.Code);

        // 未开启事务、未回冲、不写流水
        Assert.Empty(inventory.Increments);
        Assert.Empty(movements.Appended);
        Assert.DoesNotContain("Begin", calls);
        Assert.Equal(10, inventory.GetQuantity(p1.Id));
    }

    // ============================== UpdatePurchaseReturnSettlement ==============================

    [Fact]
    public async Task 更新结算_成功_应切换结算状态()
    {
        var (returns, inventory, _, user, purchaseReturn, p1, _, calls) = SeedNormal();
        var handler = new UpdatePurchaseReturnSettlementRequestHandler(returns, user);

        var result = await handler.HandleAsync(
            new UpdatePurchaseReturnSettlementRequest { Id = purchaseReturn.Id, SettlementStatus = (int)OrderSettlementStatus.Settled });

        Assert.Equal((int)OrderSettlementStatus.Settled, result.SettlementStatus);
        var (afterSettle, _) = await returns.GetDetailAsync(purchaseReturn.Id);
        Assert.Equal(OrderSettlementStatus.Settled, afterSettle!.SettlementStatus);
        Assert.Equal(user.UserId, purchaseReturn.UpdatedBy);
        Assert.Contains("UpdateSettlement", calls);
        // 库存不受结算影响
        Assert.Equal(10, inventory.GetQuantity(p1.Id));
        Assert.Empty(inventory.Increments);
    }

    [Fact]
    public async Task 更新结算_已作废_应报OrderVoided()
    {
        var (returns, _, _, user, purchaseReturn, _, _, _) = SeedNormal();
        await returns.UpdateStatusAsync(purchaseReturn.Id, OrderStatus.Voided, null);

        var handler = new UpdatePurchaseReturnSettlementRequestHandler(returns, user);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            new UpdatePurchaseReturnSettlementRequest { Id = purchaseReturn.Id, SettlementStatus = (int)OrderSettlementStatus.Settled }));
        Assert.Equal(ErrorCode.OrderVoided, ex.Code);
    }

    [Fact]
    public async Task 更新结算_不存在_应报NotFound()
    {
        var (returns, _, _, user, _, _, _, _) = SeedNormal();
        var handler = new UpdatePurchaseReturnSettlementRequestHandler(returns, user);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            new UpdatePurchaseReturnSettlementRequest { Id = Guid.NewGuid(), SettlementStatus = (int)OrderSettlementStatus.Settled }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 更新结算_目标与当前相同_应为无操作()
    {
        var (returns, _, _, user, purchaseReturn, _, _, calls) = SeedNormal();
        var handler = new UpdatePurchaseReturnSettlementRequestHandler(returns, user);

        // 当前为 Unsettled，再设为 Unsettled → 幂等，不调用 UpdateSettlementAsync
        var result = await handler.HandleAsync(
            new UpdatePurchaseReturnSettlementRequest { Id = purchaseReturn.Id, SettlementStatus = (int)OrderSettlementStatus.Unsettled });
        Assert.Equal((int)OrderSettlementStatus.Unsettled, result.SettlementStatus);
        Assert.DoesNotContain("UpdateSettlement", calls);
    }

    // ============================== GetPurchaseReturnById ==============================

    [Fact]
    public async Task 查询采购退货单详情_存在_应返回主表与明细()
    {
        var (returns, _, _, _, purchaseReturn, _, _, _) = SeedNormal();
        var handler = new GetPurchaseReturnByIdRequestHandler(returns);

        var result = await handler.HandleAsync(new GetPurchaseReturnByIdRequest { Id = purchaseReturn.Id });

        Assert.Equal(purchaseReturn.ReturnNo, result.ReturnNo);
        Assert.Equal(24.5m, result.TotalAmount);
        Assert.Equal(2, result.Items.Count);
        // 明细按插入顺序、快照字段原样返回
        Assert.Equal("商品一", result.Items[0].ProductName);
        Assert.Equal(4.5m, result.Items[0].Subtotal);
        Assert.Equal("商品二", result.Items[1].ProductName);
        Assert.Equal(20m, result.Items[1].Subtotal);
    }

    [Fact]
    public async Task 查询采购退货单详情_不存在_应报NotFound()
    {
        var (returns, _, _, _, _, _, _, _) = SeedNormal();
        var handler = new GetPurchaseReturnByIdRequestHandler(returns);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new GetPurchaseReturnByIdRequest { Id = Guid.NewGuid() }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== GetPurchaseReturns ==============================

    [Fact]
    public async Task 查询采购退货单列表_应透传筛选入参并按分页映射()
    {
        var (returns, _, _, _, purchaseReturn, _, _, _) = SeedNormal();
        purchaseReturn.Remark = "备注一";
        var handler = new GetPurchaseReturnsRequestHandler(returns);

        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.Zero);
        returns.PagedItems = new[] { purchaseReturn };
        returns.PagedTotal = 7;

        var result = await handler.HandleAsync(new GetPurchaseReturnsRequest
        {
            Page = 2,
            PageSize = 10,
            Keyword = "PR2026",
            PartnerId = purchaseReturn.PartnerId,
            Start = start,
            End = end,
            Settlement = OrderSettlementStatus.Unsettled,
        });

        // 筛选入参原样透传给仓储
        var query = Assert.Single(returns.PagedQueries);
        Assert.Equal("PR2026", query.Keyword);
        Assert.Equal(purchaseReturn.PartnerId, query.PartnerId);
        Assert.Equal(start, query.Start);
        Assert.Equal(end, query.End);
        Assert.Equal(OrderSettlementStatus.Unsettled, query.Settlement);
        Assert.Equal(2, query.Page);
        Assert.Equal(10, query.PageSize);

        // 分页结果映射（含 Status 供前端作废行置灰）
        Assert.Equal(7, result.Total);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        var row = Assert.Single(result.Items);
        Assert.Equal(purchaseReturn.ReturnNo, row.ReturnNo);
        Assert.Equal(purchaseReturn.PartnerName, row.PartnerName);
        Assert.Equal(24.5m, row.TotalAmount);
        Assert.Equal((int)OrderStatus.Normal, row.Status);
    }

    // ============================== 对账一致性（T3.6）==============================

    [Fact]
    public async Task 采购入库_退货_退货作废链路_流水累计变动量应等于当前库存()
    {
        var context = TestSupport.CreateDbContext();
        var user = new StubCurrentUser(Guid.NewGuid());
        var calls = new List<string>();
        var partner = TestSupport.NewPartner("供应商", type: PartnerType.Supplier);
        context.Partners.Add(partner);
        var product = TestSupport.NewProduct("sku-recon", "对账商品");
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var inventory = new FakeInventoryRepository(calls);
        var movements = new FakeStockMovementRepository(calls);
        var uow = new RecordingUnitOfWork(calls);
        inventory.Seed(product.Id, 0); // 期初为 0，库存变化全部由流水表达

        var purchaseOrders = new FakePurchaseOrderRepository(calls);
        var returns = new FakePurchaseReturnRepository(calls);

        var createPurchase = new CreatePurchaseOrderRequestHandler(
            purchaseOrders, new PartnerRepository(context), new ProductRepository(context),
            inventory, movements, uow, user);
        var createReturn = new CreatePurchaseReturnRequestHandler(
            returns, new PartnerRepository(context), new ProductRepository(context),
            inventory, movements, uow, user);
        var voidReturn = new VoidPurchaseReturnRequestHandler(returns, inventory, movements, uow, user);

        var inbound = await createPurchase.HandleAsync(new CreatePurchaseOrderRequest
        {
            PartnerId = partner.Id,
            OrderDate = ReturnDate,
            Items = new[] { new CreatePurchaseOrderItem { ProductId = product.Id, Quantity = 5, UnitPrice = 2m } },
        });
        var returned = await createReturn.HandleAsync(new CreatePurchaseReturnRequest
        {
            PartnerId = partner.Id,
            ReturnDate = ReturnDate,
            Items = new[] { new CreatePurchaseReturnItem { ProductId = product.Id, Quantity = 5, UnitPrice = 2m } },
        });
        await voidReturn.HandleAsync(new VoidPurchaseReturnRequest { Id = Guid.Parse(returned.Id) });

        // 三者流水：+5（采购入库）、-5（采购退货）、+5（采购退货作废）
        Assert.Equal(
            new[]
            {
                (StockMovementType.PurchaseInbound, 5),
                (StockMovementType.PurchaseReturnOut, -5),
                (StockMovementType.PurchaseReturnVoid, 5),
            },
            movements.Appended.Select(m => (m.MovementType, m.Quantity)).ToArray());

        // 对账：Σ 流水变动量 == 当前库存
        Assert.Equal(5, inventory.GetQuantity(product.Id));
        Assert.Equal(inventory.GetQuantity(product.Id), await movements.SumQuantityAsync(product.Id));

        // 单号前缀各归其域：采购入库 PO、采购退货 PR
        Assert.Matches("^PO20260101\\d{4}$", inbound.OrderNo);
        Assert.Matches("^PR20260101\\d{4}$", returned.ReturnNo);
    }
}
