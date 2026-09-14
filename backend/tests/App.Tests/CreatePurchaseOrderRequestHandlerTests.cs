using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Purchases.CreatePurchaseOrder;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// CreatePurchaseOrderRequestHandler 测试（design.md §6）：
/// 成功路径断言单号 + 后端重算 + 明细快照 + 逐行 IncrementAsync(+qty) + Commit；
/// 错误码 40110（明细空）/ 40400（供应商、商品不存在）/ 40108（供应商停用）/ 40109（类型不匹配）/ 40107（商品停用）；
/// 事务调用序列 Begin→Generate→Add→Increment→Commit；Add 失败 / Commit 失败 → Rollback。
/// 供应商 / 商品用真实仓储（纯查询 InMemory 可用）；采购单 / 库存 / 工作单元用行为型假实现（规避 InMemory 不支持 ExecuteUpdateAsync）。
/// </summary>
public class CreatePurchaseOrderRequestHandlerTests
{
    private static readonly DateTimeOffset OrderDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (AppDbContext Context, StubCurrentUser User, FakePurchaseOrderRepository Orders,
        FakeInventoryRepository Inventory, RecordingUnitOfWork Uow, CreatePurchaseOrderRequestHandler Handler,
        List<string> Calls) CreateHandler()
    {
        var context = TestSupport.CreateDbContext();
        var user = new StubCurrentUser(Guid.NewGuid());
        var calls = new List<string>();
        var orders = new FakePurchaseOrderRepository(calls);
        var inventory = new FakeInventoryRepository(calls);
        var uow = new RecordingUnitOfWork(calls);
        var handler = new CreatePurchaseOrderRequestHandler(
            orders,
            new PartnerRepository(context),
            new ProductRepository(context),
            inventory,
            uow,
            user);
        return (context, user, orders, inventory, uow, handler, calls);
    }

    private static CreatePurchaseOrderRequest RequestWith(Guid partnerId, params (Guid ProductId, int Qty, decimal Price)[] lines)
        => new()
        {
            PartnerId = partnerId,
            OrderDate = OrderDate,
            Items = lines.Select(l => new CreatePurchaseOrderItem { ProductId = l.ProductId, Quantity = l.Qty, UnitPrice = l.Price }).ToList(),
        };

    private static async Task<(Partner Partner, Product P1, Product P2)> SeedAsync(AppDbContext context)
    {
        var partner = TestSupport.NewPartner("测试供应商");
        context.Partners.Add(partner);
        var p1 = TestSupport.NewProduct("sku-po-1", "商品一");
        var p2 = TestSupport.NewProduct("sku-po-2", "商品二");
        context.Products.AddRange(p1, p2);
        await context.SaveChangesAsync();
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
    public async Task 新增采购单_成功_应生成单号并重算小计总额且逐行库存增加()
    {
        var (context, _, orders, inventory, _, handler, _) = CreateHandler();
        var (partner, p1, p2) = await SeedAsync(context);

        var result = await handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 3, 1.5m), (p2.Id, 2, 10m)));

        // 单号：PO + yyyyMMdd + 4 位序号
        Assert.Matches("^PO20260101\\d{4}$", result.OrderNo);
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

        // 逐行库存 += 数量（调用顺序与明细一致）
        Assert.Equal(
            new[] { (p1.Id, 3), (p2.Id, 2) },
            inventory.Increments.Select(i => (i.ProductId, i.Delta)).ToArray());
        Assert.Equal(3, inventory.GetQuantity(p1.Id));
        Assert.Equal(2, inventory.GetQuantity(p2.Id));
    }

    [Fact]
    public async Task 新增采购单_当天已有单据_单号序号应递增()
    {
        var (context, _, _, _, _, handler, _) = CreateHandler();
        var (partner, p1, _) = await SeedAsync(context);

        var first = await handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 1, 1m)));
        var second = await handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 1, 1m)));

        Assert.Matches("^PO20260101\\d{4}$", first.OrderNo);
        Assert.Matches("^PO20260101\\d{4}$", second.OrderNo);
        Assert.NotEqual(first.OrderNo, second.OrderNo);
        Assert.True(
            int.Parse(second.OrderNo[^4..]) > int.Parse(first.OrderNo[^4..]),
            $"单号序号应递增：{first.OrderNo} → {second.OrderNo}");
    }

    [Fact]
    public async Task 新增采购单_成功_事务调用顺序应为Begin_Generate_Add_Increment_Commit()
    {
        var (context, _, _, _, _, handler, calls) = CreateHandler();
        var (partner, p1, _) = await SeedAsync(context);

        await handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 1, 1m)));

        Assert.Equal(new[] { "Begin", "Generate", "Add", "Increment", "Commit" }, calls.ToArray());
    }

    // ============================== 供应商校验 ==============================

    [Fact]
    public async Task 新增采购单_供应商不存在_应报NotFound()
    {
        var (context, _, _, _, _, handler, _) = CreateHandler();
        var p1 = await SeedProductAsync(context, "sku-x");

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(Guid.NewGuid(), (p1.Id, 1, 1m))));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 新增采购单_供应商停用_应报PartnerDisabled()
    {
        var (context, _, _, _, _, handler, _) = CreateHandler();
        var partner = TestSupport.NewPartner("停用供应商", status: PartnerStatus.Disabled);
        context.Partners.Add(partner);
        var p1 = await SeedProductAsync(context, "sku-off-p");
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(partner.Id, (p1.Id, 1, 1m))));
        Assert.Equal(ErrorCode.PartnerDisabled, ex.Code);
    }

    [Fact]
    public async Task 新增采购单_纯客户开采购单_应报PartnerTypeMismatch()
    {
        var (context, _, _, _, _, handler, _) = CreateHandler();
        var partner = TestSupport.NewPartner("纯客户", type: PartnerType.Customer);
        context.Partners.Add(partner);
        var p1 = await SeedProductAsync(context, "sku-cust");
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(partner.Id, (p1.Id, 1, 1m))));
        Assert.Equal(ErrorCode.PartnerTypeMismatch, ex.Code);
    }

    [Fact]
    public async Task 新增采购单_两者类型_应通过类型校验()
    {
        var (context, _, _, _, _, handler, _) = CreateHandler();
        var partner = TestSupport.NewPartner("两者单位", type: PartnerType.Both);
        context.Partners.Add(partner);
        var p1 = await SeedProductAsync(context, "sku-both");
        await context.SaveChangesAsync();

        var result = await handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 1, 1m)));
        Assert.Equal((int)OrderStatus.Normal, result.Status);
    }

    // ============================== 商品校验 ==============================

    [Fact]
    public async Task 新增采购单_商品不存在_应报NotFound()
    {
        var (context, _, _, _, _, handler, _) = CreateHandler();
        var partner = TestSupport.NewPartner("供应商");
        context.Partners.Add(partner);
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(partner.Id, (Guid.NewGuid(), 1, 1m))));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 新增采购单_商品停用_应报ProductDisabled()
    {
        var (context, _, _, _, _, handler, _) = CreateHandler();
        var partner = TestSupport.NewPartner("供应商");
        context.Partners.Add(partner);
        var p1 = await SeedProductAsync(context, "sku-off", status: ProductStatus.Disabled);
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(partner.Id, (p1.Id, 1, 1m))));
        Assert.Equal(ErrorCode.ProductDisabled, ex.Code);
    }

    [Fact]
    public async Task 新增采购单_明细为空_应报OrderItemsEmpty()
    {
        var (context, _, _, _, _, handler, _) = CreateHandler();
        var partner = TestSupport.NewPartner("供应商");
        context.Partners.Add(partner);
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreatePurchaseOrderRequest
        {
            PartnerId = partner.Id,
            OrderDate = OrderDate,
            Items = Array.Empty<CreatePurchaseOrderItem>(),
        }));
        Assert.Equal(ErrorCode.OrderItemsEmpty, ex.Code);
    }

    // ============================== 事务失败回滚 ==============================

    [Fact]
    public async Task 新增采购单_Add失败_应回滚且不提交()
    {
        var (context, _, orders, _, uow, handler, calls) = CreateHandler();
        var (partner, p1, _) = await SeedAsync(context);
        orders.AddFailure = () => new InvalidOperationException("模拟数据库写入失败");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 1, 1m))));
        Assert.Contains("模拟数据库写入失败", ex.Message);

        // Begin → Rollback，且从未 Commit
        Assert.Equal(new[] { "Begin", "Generate", "Add", "Rollback" }, calls.ToArray());
        Assert.DoesNotContain("Commit", calls);
    }

    [Fact]
    public async Task 新增采购单_Commit失败_应回滚且异常上抛()
    {
        var (context, _, _, _, uow, handler, calls) = CreateHandler();
        var (partner, p1, _) = await SeedAsync(context);
        uow.CommitFailure = () => new InvalidOperationException("模拟提交失败");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 1, 1m))));
        Assert.Contains("模拟提交失败", ex.Message);

        // Begin → Generate → Add → Increment → Commit(抛) → Rollback，异常上抛
        Assert.Equal(new[] { "Begin", "Generate", "Add", "Increment", "Commit", "Rollback" }, calls.ToArray());
    }
}
