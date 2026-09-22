using App.Core;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.SalesReturns.CreateSalesReturn;
using App.Core.Features.SalesReturns.GetSalesReturnById;
using App.Core.Features.SalesReturns.GetSalesReturns;
using App.Core.Features.SalesReturns.VoidSalesReturn;
using App.Core.Features.SalesShipments.CreateSalesShipment;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 销售退货单生命周期用例测试（design.md §6）：
/// VoidSalesReturn（成功逐行回冲 IncrementAsync(-q) + 负方向流水 + UpdateStatusAsync(Voided)；允许冲负；不存在 40400；已作废 40104 且不重复回冲）；
/// 结算状态推导（specs/023-erp-settlement §0：SettledAmount ≤ 0 / 部分 / ≥ 总额 三态）；
/// GetSalesReturnById（存在含明细映射；不存在 40400）；GetSalesReturns（客户维度筛选传参 + 分页映射）；
/// 对账一致性（销售出库 → 退货 → 退货作废链路后 Σ 流水变动量 == Inventory.Quantity）。
/// 单据 / 库存 / 流水 / 工作单元用行为型假实现（规避 InMemory 不支持 ExecuteUpdateAsync）。
/// </summary>
public class SalesReturnLifecycleTests
{
    private static readonly DateTimeOffset ReturnDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 构造一张正常销售退货单（2 行明细：3×1.5 + 2×10 = 24.5）+ 商品 + 库存（10 / 8），预置进假仓储。
    /// </summary>
    private static (FakeSalesReturnRepository Returns, FakeInventoryRepository Inventory,
        RecordingUnitOfWork Uow, StubCurrentUser User, SalesReturn SalesReturn, Product P1, Product P2, List<string> Calls) SeedNormal()
    {
        var calls = new List<string>();
        var returns = new FakeSalesReturnRepository(calls);
        var inventory = new FakeInventoryRepository(calls);
        var uow = new RecordingUnitOfWork(calls);
        var user = new StubCurrentUser(Guid.NewGuid());

        var partner = TestSupport.NewPartner("客户");
        var p1 = TestSupport.NewProduct("sku-sr-v1", "商品一");
        var p2 = TestSupport.NewProduct("sku-sr-v2", "商品二");
        inventory.Seed(p1.Id, 10);
        inventory.Seed(p2.Id, 8);

        var now = DateTimeOffset.UtcNow;
        var salesReturn = new SalesReturn
        {
            Id = Guid.NewGuid(),
            ReturnNo = "SR202601010001",
            PartnerId = partner.Id,
            PartnerName = partner.Name,
            ReturnDate = ReturnDate,
            TotalAmount = 24.5m,
            SettledAmount = 0m,
            Status = OrderStatus.Normal,
            CreatedAt = now,
            UpdatedAt = now,
        };
        // 明细 Id 用顺序 Guid（与生产一致），保证按 Id 排序还原插入顺序
        var items = new List<SalesReturnItem>
        {
            new() { Id = SequentialGuidGenerator.NewSequential(), ReturnId = salesReturn.Id, ProductId = p1.Id, ProductName = p1.Name, Unit = p1.Unit, Quantity = 3, UnitPrice = 1.5m, Subtotal = 4.5m },
            new() { Id = SequentialGuidGenerator.NewSequential(), ReturnId = salesReturn.Id, ProductId = p2.Id, ProductName = p2.Name, Unit = p2.Unit, Quantity = 2, UnitPrice = 10m, Subtotal = 20m },
        };
        returns.Seed(salesReturn, items);

        return (returns, inventory, uow, user, salesReturn, p1, p2, calls);
    }

    // ============================== VoidSalesReturn ==============================

    [Fact]
    public async Task 作废销售退货单_成功_应逐行回冲库存并置作废()
    {
        var (returns, inventory, uow, user, salesReturn, p1, p2, calls) = SeedNormal();
        var movements = new FakeStockMovementRepository(calls);
        var handler = new VoidSalesReturnRequestHandler(returns, inventory, movements, uow, user, TestSupport.AuditLogger);

        var result = await handler.HandleAsync(new VoidSalesReturnRequest { Id = salesReturn.Id });

        // 回冲：逐行库存 -= 退货数量（撤销保存时的回增），调用顺序与明细一致
        Assert.Equal(
            new[] { (p1.Id, -3), (p2.Id, -2) },
            inventory.Increments.Select(i => (i.ProductId, i.Delta)).ToArray());
        Assert.Equal(7, inventory.GetQuantity(p1.Id));  // 10 - 3
        Assert.Equal(6, inventory.GetQuantity(p2.Id));  // 8 - 2

        // 逐行追加销售退货作废流水：类型 / 负方向 / 来源 / 操作人
        Assert.Equal(2, movements.Appended.Count);
        Assert.Equal(new[] { (p1.Id, -3), (p2.Id, -2) }, movements.Appended.Select(m => (m.ProductId, m.Quantity)).ToArray());
        Assert.All(movements.Appended, m =>
        {
            Assert.Equal(StockMovementType.SalesReturnVoid, m.MovementType);
            Assert.Equal(user.UserId, m.CreatedBy);
            Assert.Equal(salesReturn.Id, m.SourceId);
            Assert.Equal(salesReturn.ReturnNo, m.SourceNo);
        });
        // 状态置作废（事务序列：Begin → Increment / Append 交替 → UpdateStatus → Commit）
        var (afterVoid, _) = await returns.GetDetailAsync(salesReturn.Id);
        Assert.Equal(OrderStatus.Voided, afterVoid!.Status);
        Assert.Equal(user.UserId, salesReturn.UpdatedBy);
        Assert.Equal((int)OrderStatus.Voided, result.Status);
        // 成本：销售退货作废按原入库单价回冲（ApplyOutboundCost），与数量回冲同事务
        Assert.Equal(new[] { "Begin", "Increment", "ApplyOutboundCost", "Append", "Increment", "ApplyOutboundCost", "Append", "UpdateStatus", "Commit" }, calls.ToArray());
    }

    [Fact]
    public async Task 作废销售退货单_库存不足冲减_应允许冲负()
    {
        var (returns, inventory, uow, user, salesReturn, p1, p2, _) = SeedNormal();
        // 退回入库的货已被再次卖出：库存低于退货数量
        inventory.Seed(p1.Id, 1);
        inventory.Seed(p2.Id, 0);
        var handler = new VoidSalesReturnRequestHandler(returns, inventory, new FakeStockMovementRepository(), uow, user, TestSupport.AuditLogger);

        await handler.HandleAsync(new VoidSalesReturnRequest { Id = salesReturn.Id });

        // 允许冲负：作废必须可执行（design.md §5 决策），库存为负由库存页标红呈现
        Assert.Equal(-2, inventory.GetQuantity(p1.Id)); // 1 - 3
        Assert.Equal(-2, inventory.GetQuantity(p2.Id)); // 0 - 2
    }

    [Fact]
    public async Task 作废销售退货单_不存在_应报NotFound()
    {
        var (returns, inventory, uow, user, _, _, _, _) = SeedNormal();
        var handler = new VoidSalesReturnRequestHandler(returns, inventory, new FakeStockMovementRepository(), uow, user, TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new VoidSalesReturnRequest { Id = Guid.NewGuid() }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 作废销售退货单_已作废_应报OrderVoided且不重复回冲()
    {
        var (returns, inventory, uow, user, salesReturn, p1, _, calls) = SeedNormal();
        // 预置为已作废
        await returns.UpdateStatusAsync(salesReturn.Id, OrderStatus.Voided, null);

        var movements = new FakeStockMovementRepository(calls);
        var handler = new VoidSalesReturnRequestHandler(returns, inventory, movements, uow, user, TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new VoidSalesReturnRequest { Id = salesReturn.Id }));
        Assert.Equal(ErrorCode.OrderVoided, ex.Code);

        // 未开启事务、未回冲、不写流水
        Assert.Empty(inventory.Increments);
        Assert.Empty(movements.Appended);
        Assert.DoesNotContain("Begin", calls);
        Assert.Equal(10, inventory.GetQuantity(p1.Id));
    }

    [Fact]
    public async Task 作废销售退货单_已核销_应报OrderSettledCannotVoid且不回冲()
    {
        var (returns, inventory, uow, user, salesReturn, p1, _, calls) = SeedNormal();
        salesReturn.SettledAmount = 10m; // 已被付款单核销（部分）

        var movements = new FakeStockMovementRepository(calls);
        var handler = new VoidSalesReturnRequestHandler(returns, inventory, movements, uow, user, TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new VoidSalesReturnRequest { Id = salesReturn.Id }));
        Assert.Equal(ErrorCode.OrderSettledCannotVoid, ex.Code);
        Assert.Contains(salesReturn.ReturnNo, ex.Message);
        Assert.Contains("10.00", ex.Message);

        // 作废与核销互斥：未开启事务、未回冲库存、不写流水、状态保持正常
        Assert.Empty(inventory.Increments);
        Assert.Empty(movements.Appended);
        Assert.DoesNotContain("Begin", calls);
        Assert.Equal(10, inventory.GetQuantity(p1.Id));
        var (afterVoid, _) = await returns.GetDetailAsync(salesReturn.Id);
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
        var (returns, _, _, _, salesReturn, _, _, _) = SeedNormal();
        salesReturn.SettledAmount = settledAmount;
        var handler = new GetSalesReturnByIdRequestHandler(returns);

        var result = await handler.HandleAsync(new GetSalesReturnByIdRequest { Id = salesReturn.Id });

        // 0 未结算 / 1 部分结算 / 2 已结算（SettledAmount ≥ TotalAmount 视为结清）；未结金额 = 总额 − 已结
        Assert.Equal((int)expected, result.SettlementState);
        Assert.Equal(salesReturn.TotalAmount - settledAmount, result.UnsettledAmount);
        Assert.Equal(settledAmount, result.SettledAmount);
    }

    // ============================== GetSalesReturnById ==============================

    [Fact]
    public async Task 查询销售退货单详情_存在_应返回主表与明细()
    {
        var (returns, _, _, _, salesReturn, _, _, _) = SeedNormal();
        var handler = new GetSalesReturnByIdRequestHandler(returns);

        var result = await handler.HandleAsync(new GetSalesReturnByIdRequest { Id = salesReturn.Id });

        Assert.Equal(salesReturn.ReturnNo, result.ReturnNo);
        Assert.Equal(24.5m, result.TotalAmount);
        Assert.Equal(2, result.Items.Count);
        // 明细按插入顺序、快照字段原样返回
        Assert.Equal("商品一", result.Items[0].ProductName);
        Assert.Equal(4.5m, result.Items[0].Subtotal);
        Assert.Equal("商品二", result.Items[1].ProductName);
        Assert.Equal(20m, result.Items[1].Subtotal);
    }

    [Fact]
    public async Task 查询销售退货单详情_不存在_应报NotFound()
    {
        var (returns, _, _, _, _, _, _, _) = SeedNormal();
        var handler = new GetSalesReturnByIdRequestHandler(returns);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new GetSalesReturnByIdRequest { Id = Guid.NewGuid() }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== GetSalesReturns ==============================

    [Fact]
    public async Task 查询销售退货单列表_应透传客户维度筛选入参并按分页映射()
    {
        var (returns, _, _, _, salesReturn, _, _, _) = SeedNormal();
        salesReturn.Remark = "备注一";
        var handler = new GetSalesReturnsRequestHandler(returns);

        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.Zero);
        returns.PagedItems = new[] { salesReturn };
        returns.PagedTotal = 3;

        var result = await handler.HandleAsync(new GetSalesReturnsRequest
        {
            Page = 2,
            PageSize = 10,
            Keyword = "SR2026",
            PartnerId = salesReturn.PartnerId,
            Start = start,
            End = end,
            SettlementState = SettlementState.Unsettled,
        });

        // 筛选入参原样透传给仓储
        var query = Assert.Single(returns.PagedQueries);
        Assert.Equal("SR2026", query.Keyword);
        Assert.Equal(salesReturn.PartnerId, query.PartnerId);
        Assert.Equal(start, query.Start);
        Assert.Equal(end, query.End);
        Assert.Equal(SettlementState.Unsettled, query.SettlementState);
        Assert.Equal(2, query.Page);
        Assert.Equal(10, query.PageSize);

        // 分页结果映射（含 Status 供前端作废行置灰）
        Assert.Equal(3, result.Total);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        var row = Assert.Single(result.Items);
        Assert.Equal(salesReturn.ReturnNo, row.ReturnNo);
        Assert.Equal(salesReturn.PartnerName, row.PartnerName);
        Assert.Equal(24.5m, row.TotalAmount);
        Assert.Equal((int)OrderStatus.Normal, row.Status);
    }

    // ============================== 对账一致性（T3.6）==============================

    [Fact]
    public async Task 销售出库_退货_退货作废链路_流水累计变动量应等于当前库存()
    {
        var context = TestSupport.CreateDbContext();
        var user = new StubCurrentUser(Guid.NewGuid());
        var calls = new List<string>();
        var partner = TestSupport.NewPartner("客户", type: PartnerType.Customer);
        context.Partners.Add(partner);
        var product = TestSupport.NewProduct("sku-sr-recon", "对账商品");
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var inventory = new FakeInventoryRepository(calls);
        var movements = new FakeStockMovementRepository(calls);
        var uow = new RecordingUnitOfWork(calls);
        inventory.Seed(product.Id, 0); // 期初为 0，库存变化全部由流水表达

        var salesShipments = new FakeSalesShipmentRepository(calls);
        var returns = new FakeSalesReturnRepository(calls);

        // 先入库 5（期初），再销售出库 2、退货 2、退货作废 2
        var salesCreate = new CreateSalesShipmentRequestHandler(
            salesShipments, new FakeSalesOrderRepository(calls), new PartnerRepository(context), new ProductRepository(context),
            inventory, movements, uow, user, TestSupport.AuditLogger);
        var returnCreate = new CreateSalesReturnRequestHandler(
            returns, new PartnerRepository(context), new ProductRepository(context),
            inventory, movements, uow, user, TestSupport.AuditLogger);
        var returnVoid = new VoidSalesReturnRequestHandler(returns, inventory, movements, uow, user, TestSupport.AuditLogger);

        inventory.Seed(product.Id, 5); // 期初库存（无流水，模拟开账前已存在）

        var sold = await salesCreate.HandleAsync(new CreateSalesShipmentRequest
        {
            PartnerId = partner.Id,
            OrderDate = ReturnDate,
            Items = new[] { new CreateSalesShipmentItem { ProductId = product.Id, Quantity = 2, UnitPrice = 10m } },
        });
        var returned = await returnCreate.HandleAsync(new CreateSalesReturnRequest
        {
            PartnerId = partner.Id,
            ReturnDate = ReturnDate,
            Items = new[] { new CreateSalesReturnItem { ProductId = product.Id, Quantity = 2, UnitPrice = 10m } },
        });
        await returnVoid.HandleAsync(new VoidSalesReturnRequest { Id = Guid.Parse(returned.Id) });

        // 流水：-2（销售出库）、+2（销售退货）、-2（销售退货作废）
        Assert.Equal(
            new[]
            {
                (StockMovementType.SalesOutbound, -2),
                (StockMovementType.SalesReturnIn, 2),
                (StockMovementType.SalesReturnVoid, -2),
            },
            movements.Appended.Select(m => (m.MovementType, m.Quantity)).ToArray());

        // 对账：期初 5 + Σ 流水变动量 == 当前库存
        Assert.Equal(3, inventory.GetQuantity(product.Id)); // 5 - 2 + 2 - 2
        Assert.Equal(5 + await movements.SumQuantityAsync(product.Id), inventory.GetQuantity(product.Id));

        // 单号前缀各归其域：销售出库 GI、销售退货 SR
        Assert.Matches("^GI20260101\\d{4}$", sold.ShipmentNo);
        Assert.Matches("^SR20260101\\d{4}$", returned.ReturnNo);
    }
}
