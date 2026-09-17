using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.SalesReturns.CreateSalesReturn;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// CreateSalesReturnRequestHandler 测试（design.md §6）：
/// 成功路径断言单号 SR + 后端重算 + 明细快照 + 逐行回增库存（IncrementAsync(+q)）+ 逐行流水（SalesReturnIn 正方向）+ Commit；
/// 事务调用顺序 Begin→Generate→Add→Increment→Append→Commit（销售退货无库存约束，插单先于回增）；
/// 错误码 40110（明细空）/ 40400（客户、商品不存在）/ 40108（客户停用）/ 40109（类型不匹配）/ 40107（商品停用）；
/// 失败路径不写流水、不写库存、RollbackAsync 断言。`40103` 不参与（退货回增库存无上限校验）。
/// 客户 / 商品用真实仓储（纯查询 InMemory 可用）；退货单 / 库存 / 流水 / 工作单元用行为型假实现。
/// </summary>
public class CreateSalesReturnRequestHandlerTests
{
    private static readonly DateTimeOffset ReturnDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (AppDbContext Context, StubCurrentUser User, FakeSalesReturnRepository Returns,
        FakeInventoryRepository Inventory, FakeStockMovementRepository Movements, RecordingUnitOfWork Uow,
        CreateSalesReturnRequestHandler Handler, List<string> Calls) CreateHandler()
    {
        var context = TestSupport.CreateDbContext();
        var user = new StubCurrentUser(Guid.NewGuid());
        var calls = new List<string>();
        var returns = new FakeSalesReturnRepository(calls);
        var inventory = new FakeInventoryRepository(calls);
        var movements = new FakeStockMovementRepository(calls);
        var uow = new RecordingUnitOfWork(calls);
        var handler = new CreateSalesReturnRequestHandler(
            returns,
            new PartnerRepository(context),
            new ProductRepository(context),
            inventory,
            movements,
            uow,
            user);
        return (context, user, returns, inventory, movements, uow, handler, calls);
    }

    private static CreateSalesReturnRequest RequestWith(Guid partnerId, params (Guid ProductId, int Qty, decimal Price)[] lines)
        => new()
        {
            PartnerId = partnerId,
            ReturnDate = ReturnDate,
            Items = lines.Select(l => new CreateSalesReturnItem { ProductId = l.ProductId, Quantity = l.Qty, UnitPrice = l.Price }).ToList(),
        };

    private static async Task<(Partner Partner, Product P1, Product P2)> SeedAsync(AppDbContext context, int stock1 = 0, int stock2 = 0, FakeInventoryRepository? inventory = null)
    {
        var partner = TestSupport.NewPartner("测试客户", type: PartnerType.Customer);
        context.Partners.Add(partner);
        var p1 = TestSupport.NewProduct("sku-sr-1", "商品一");
        var p2 = TestSupport.NewProduct("sku-sr-2", "商品二");
        context.Products.AddRange(p1, p2);
        await context.SaveChangesAsync();
        inventory?.Seed(p1.Id, stock1);
        inventory?.Seed(p2.Id, stock2);
        return (partner, p1, p2);
    }

    private static async Task<Product> SeedProductAsync(AppDbContext context, string code, ProductStatus status = ProductStatus.Enabled)
    {
        var product = TestSupport.NewProduct(code, code, 1m, status: status);
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    // ============================== 成功路径 ==============================

    [Fact]
    public async Task 新增销售退货单_成功_应生成单号并重算小计总额且逐行库存回增()
    {
        var (context, user, _, inventory, movements, _, handler, _) = CreateHandler();
        var (partner, p1, p2) = await SeedAsync(context, stock1: 1, stock2: 2, inventory: inventory);

        var result = await handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 3, 1.5m), (p2.Id, 2, 10m)));

        // 单号：SR + yyyyMMdd + 4 位序号
        Assert.Matches("^SR20260101\\d{4}$", result.ReturnNo);
        Assert.Equal((int)OrderSettlementStatus.Unsettled, result.SettlementStatus);
        Assert.Equal((int)OrderStatus.Normal, result.Status);
        Assert.Equal(partner.Name, result.PartnerName);

        // 后端重算：小计 = 数量 × 单价，总额 = Σ 小计（不信任前端传值）
        Assert.Equal(4.5m, result.Items[0].Subtotal);   // 3 × 1.5
        Assert.Equal(20m, result.Items[1].Subtotal);     // 2 × 10
        Assert.Equal(24.5m, result.TotalAmount);

        // 明细快照取自商品档案（名称 / 单位）
        Assert.Equal(p1.Name, result.Items[0].ProductName);
        Assert.Equal(p1.Unit, result.Items[0].Unit);

        // 逐行回增 = 数量（调用顺序与明细一致）
        Assert.Equal(
            new[] { (p1.Id, 3), (p2.Id, 2) },
            inventory.Increments.Select(i => (i.ProductId, i.Delta)).ToArray());
        Assert.Equal(4, inventory.GetQuantity(p1.Id));   // 1 + 3
        Assert.Equal(4, inventory.GetQuantity(p2.Id));   // 2 + 2

        // 逐行追加销售退货流水：类型 / 正方向 / 来源 / 操作人
        Assert.Equal(2, movements.Appended.Count);
        Assert.All(movements.Appended, m =>
        {
            Assert.Equal(StockMovementType.SalesReturnIn, m.MovementType);
            Assert.Equal(user.UserId, m.CreatedBy);
            Assert.Equal(Guid.Parse(result.Id), m.SourceId);
            Assert.Equal(result.ReturnNo, m.SourceNo);
        });
        Assert.Equal(new[] { (p1.Id, 3), (p2.Id, 2) }, movements.Appended.Select(m => (m.ProductId, m.Quantity)).ToArray());

        // 对账一致性：Σ 流水变动量 == 库存净变动量（种子库存不入流水）
        Assert.Equal(3, await movements.SumQuantityAsync(p1.Id));
        Assert.Equal(2, await movements.SumQuantityAsync(p2.Id));
    }

    [Fact]
    public async Task 新增销售退货单_库存为0_应可回增且无上限校验()
    {
        var (context, _, _, inventory, _, _, handler, _) = CreateHandler();
        var (partner, p1, _) = await SeedAsync(context, inventory: inventory); // 库存 0

        var result = await handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 999999, 1m)));

        Assert.Equal(999999, inventory.GetQuantity(p1.Id));
        Assert.Equal(999999m, result.TotalAmount);
    }

    [Fact]
    public async Task 新增销售退货单_当天已有单据_单号序号应递增()
    {
        var (context, _, _, inventory, _, _, handler, _) = CreateHandler();
        var (partner, p1, _) = await SeedAsync(context, inventory: inventory);

        var first = await handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 1, 1m)));
        var second = await handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 1, 1m)));

        Assert.Matches("^SR20260101\\d{4}$", first.ReturnNo);
        Assert.Matches("^SR20260101\\d{4}$", second.ReturnNo);
        Assert.NotEqual(first.ReturnNo, second.ReturnNo);
        Assert.True(
            int.Parse(second.ReturnNo[^4..]) > int.Parse(first.ReturnNo[^4..]),
            $"单号序号应递增：{first.ReturnNo} → {second.ReturnNo}");
    }

    [Fact]
    public async Task 新增销售退货单_成功_事务调用顺序应为Begin_Generate_Add_Increment_Append_Commit()
    {
        var (context, _, _, inventory, _, _, handler, calls) = CreateHandler();
        var (partner, p1, _) = await SeedAsync(context, inventory: inventory);

        await handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 1, 1m)));

        // 销售退货无库存约束：生成单号 → 插单 → 回增 + 流水（design.md §3.5）
        Assert.Equal(new[] { "Begin", "Generate", "Add", "Increment", "Append", "Commit" }, calls.ToArray());
    }

    [Fact]
    public async Task 新增销售退货单_备注为空白_应落库为空()
    {
        var (context, _, _, inventory, _, _, handler, _) = CreateHandler();
        var (partner, p1, _) = await SeedAsync(context, inventory: inventory);

        var result = await handler.HandleAsync(new CreateSalesReturnRequest
        {
            PartnerId = partner.Id,
            ReturnDate = ReturnDate,
            Items = new[] { new CreateSalesReturnItem { ProductId = p1.Id, Quantity = 1, UnitPrice = 1m } },
            Remark = "   ",
        });

        Assert.Null(result.Remark);
    }

    // ============================== 客户校验 ==============================

    [Fact]
    public async Task 新增销售退货单_客户不存在_应报NotFound()
    {
        var (context, _, _, _, _, _, handler, _) = CreateHandler();
        await SeedProductAsync(context, "sku-x");

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(Guid.NewGuid(), (Guid.NewGuid(), 1, 1m))));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 新增销售退货单_客户停用_应报PartnerDisabled()
    {
        var (context, _, _, _, _, _, handler, _) = CreateHandler();
        var partner = TestSupport.NewPartner("停用客户", type: PartnerType.Customer, status: PartnerStatus.Disabled);
        context.Partners.Add(partner);
        var p1 = await SeedProductAsync(context, "sku-off-p");
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(partner.Id, (p1.Id, 1, 1m))));
        Assert.Equal(ErrorCode.PartnerDisabled, ex.Code);
    }

    [Fact]
    public async Task 新增销售退货单_纯供应商开退货单_应报PartnerTypeMismatch()
    {
        var (context, _, _, _, _, _, handler, _) = CreateHandler();
        var partner = TestSupport.NewPartner("纯供应商", type: PartnerType.Supplier);
        context.Partners.Add(partner);
        var p1 = await SeedProductAsync(context, "sku-sup");
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(partner.Id, (p1.Id, 1, 1m))));
        Assert.Equal(ErrorCode.PartnerTypeMismatch, ex.Code);
    }

    [Fact]
    public async Task 新增销售退货单_两者类型_应通过类型校验()
    {
        var (context, _, _, inventory, _, _, handler, _) = CreateHandler();
        var partner = TestSupport.NewPartner("两者单位", type: PartnerType.Both);
        context.Partners.Add(partner);
        var p1 = await SeedProductAsync(context, "sku-both");
        await context.SaveChangesAsync();
        inventory.Seed(p1.Id, 0);

        var result = await handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 1, 1m)));
        Assert.Equal((int)OrderStatus.Normal, result.Status);
    }

    // ============================== 商品校验 ==============================

    [Fact]
    public async Task 新增销售退货单_商品不存在_应报NotFound()
    {
        var (context, _, _, _, _, _, handler, _) = CreateHandler();
        var partner = TestSupport.NewPartner("客户", type: PartnerType.Customer);
        context.Partners.Add(partner);
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(partner.Id, (Guid.NewGuid(), 1, 1m))));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 新增销售退货单_商品停用_应报ProductDisabled()
    {
        var (context, _, _, _, _, _, handler, _) = CreateHandler();
        var partner = TestSupport.NewPartner("客户", type: PartnerType.Customer);
        context.Partners.Add(partner);
        var p1 = await SeedProductAsync(context, "sku-off", status: ProductStatus.Disabled);
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(partner.Id, (p1.Id, 1, 1m))));
        Assert.Equal(ErrorCode.ProductDisabled, ex.Code);
    }

    [Fact]
    public async Task 新增销售退货单_明细为空_应报OrderItemsEmpty()
    {
        var (context, _, _, _, _, _, handler, _) = CreateHandler();
        var partner = TestSupport.NewPartner("客户", type: PartnerType.Customer);
        context.Partners.Add(partner);
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateSalesReturnRequest
        {
            PartnerId = partner.Id,
            ReturnDate = ReturnDate,
            Items = Array.Empty<CreateSalesReturnItem>(),
        }));
        Assert.Equal(ErrorCode.OrderItemsEmpty, ex.Code);
    }

    [Fact]
    public async Task 新增销售退货单_商品校验失败_不应写库存与流水()
    {
        var (context, _, _, inventory, movements, _, handler, calls) = CreateHandler();
        var partner = TestSupport.NewPartner("客户", type: PartnerType.Customer);
        context.Partners.Add(partner);
        var p1 = await SeedProductAsync(context, "sku-mix");
        var disabled = await SeedProductAsync(context, "sku-mix-off", status: ProductStatus.Disabled);
        await context.SaveChangesAsync();
        inventory.Seed(p1.Id, 0);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(partner.Id, (p1.Id, 2, 1m), (disabled.Id, 1, 1m))));

        Assert.Equal(ErrorCode.ProductDisabled, ex.Code);
        // 校验阶段即失败：未开事务、未写库存、不写流水
        Assert.Empty(inventory.Increments);
        Assert.Empty(movements.Appended);
        Assert.DoesNotContain("Begin", calls);
        Assert.Equal(0, inventory.GetQuantity(p1.Id));
    }

    // ============================== 事务失败回滚 ==============================

    [Fact]
    public async Task 新增销售退货单_Add失败_应回滚且不写流水()
    {
        var (context, _, returns, inventory, movements, _, handler, calls) = CreateHandler();
        var (partner, p1, _) = await SeedAsync(context, inventory: inventory);
        returns.AddFailure = () => new InvalidOperationException("模拟数据库写入失败");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 1, 1m))));
        Assert.Contains("模拟数据库写入失败", ex.Message);

        // Begin → Generate → Add → Rollback，且从未 Commit；Add 失败不回增库存、不写流水
        Assert.Equal(new[] { "Begin", "Generate", "Add", "Rollback" }, calls.ToArray());
        Assert.DoesNotContain("Commit", calls);
        Assert.Empty(inventory.Increments);
        Assert.Empty(movements.Appended);
    }

    [Fact]
    public async Task 新增销售退货单_Commit失败_应回滚且异常上抛()
    {
        var (context, _, _, inventory, _, uow, handler, calls) = CreateHandler();
        var (partner, p1, _) = await SeedAsync(context, inventory: inventory);
        uow.CommitFailure = () => new InvalidOperationException("模拟提交失败");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 1, 1m))));
        Assert.Contains("模拟提交失败", ex.Message);

        // Begin → Generate → Add → Increment → Append → Commit(抛) → Rollback，异常上抛
        Assert.Equal(new[] { "Begin", "Generate", "Add", "Increment", "Append", "Commit", "Rollback" }, calls.ToArray());
    }
}
