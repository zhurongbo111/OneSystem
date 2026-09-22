using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.SalesOrders.CreateSalesOrder;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// CreateSalesOrderRequestHandler 测试（design.md §6，与采购订单同构）：
/// 成功路径断言单号 SO + 后端重算 + 明细快照 + 状态待发货 + 累计已发为 0 + 事务序列 Begin→Generate→Add→Commit；
/// **订单不触碰库存与库存流水**；错误码 40110 / 40400 / 40108 / 40109 / 40107；Add 失败 / Commit 失败 → Rollback。
/// </summary>
public class CreateSalesOrderRequestHandlerTests
{
    private static readonly DateTimeOffset OrderDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (AppDbContext Context, StubCurrentUser User, FakeSalesOrderRepository Orders,
        RecordingUnitOfWork Uow, CreateSalesOrderRequestHandler Handler, List<string> Calls) CreateHandler()
    {
        var context = TestSupport.CreateDbContext();
        var user = new StubCurrentUser(Guid.NewGuid());
        var calls = new List<string>();
        var orders = new FakeSalesOrderRepository(calls);
        var uow = new RecordingUnitOfWork(calls);
        var handler = new CreateSalesOrderRequestHandler(
            orders,
            new PartnerRepository(context),
            new ProductRepository(context),
            uow,
            user, TestSupport.AuditLogger);
        return (context, user, orders, uow, handler, calls);
    }

    private static CreateSalesOrderRequest RequestWith(Guid partnerId, params (Guid ProductId, int Qty, decimal Price)[] lines)
        => new()
        {
            PartnerId = partnerId,
            OrderDate = OrderDate,
            Items = lines.Select(l => new CreateSalesOrderItem { ProductId = l.ProductId, Quantity = l.Qty, UnitPrice = l.Price }).ToList(),
        };

    private static async Task<(Partner Partner, Product P1, Product P2)> SeedAsync(AppDbContext context)
    {
        var partner = TestSupport.NewPartner("测试客户", type: PartnerType.Customer);
        context.Partners.Add(partner);
        var p1 = TestSupport.NewProduct("sku-so-1", "商品一");
        var p2 = TestSupport.NewProduct("sku-so-2", "商品二");
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
    public async Task 新增销售订单_成功_应生成单号并重算金额且状态为待发货()
    {
        var (context, _, _, _, handler, calls) = CreateHandler();
        var (partner, p1, p2) = await SeedAsync(context);

        var result = await handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 3, 1.5m), (p2.Id, 2, 10m)));

        Assert.Matches("^SO20260101\\d{4}$", result.OrderNo);
        Assert.Equal((int)OrderFlowStatus.Pending, result.FlowStatus);
        Assert.Equal(partner.Name, result.PartnerName);

        Assert.Equal(4.5m, result.Items[0].Subtotal);
        Assert.Equal(20m, result.Items[1].Subtotal);
        Assert.Equal(24.5m, result.TotalAmount);

        Assert.Equal(p1.Name, result.Items[0].ProductName);
        Assert.All(result.Items, item =>
        {
            Assert.Equal(0, item.FulfilledQuantity);
            Assert.Equal(item.Quantity, item.RemainingQuantity);
        });

        // 计划单据：不触碰库存 / 流水
        Assert.Equal(new[] { "Begin", "Generate", "Add", "Commit" }, calls.ToArray());
    }

    [Fact]
    public async Task 新增销售订单_当天已有订单_单号序号应递增()
    {
        var (context, _, _, _, handler, _) = CreateHandler();
        var (partner, p1, _) = await SeedAsync(context);

        var first = await handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 1, 1m)));
        var second = await handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 1, 1m)));

        Assert.True(
            int.Parse(second.OrderNo[^4..]) > int.Parse(first.OrderNo[^4..]),
            $"单号序号应递增：{first.OrderNo} → {second.OrderNo}");
    }

    // ============================== 客户 / 商品校验 ==============================

    [Fact]
    public async Task 新增销售订单_明细为空_应报OrderItemsEmpty()
    {
        var (context, _, _, _, handler, _) = CreateHandler();
        var partner = TestSupport.NewPartner("客户");
        context.Partners.Add(partner);
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateSalesOrderRequest
        {
            PartnerId = partner.Id,
            OrderDate = OrderDate,
            Items = Array.Empty<CreateSalesOrderItem>(),
        }));
        Assert.Equal(ErrorCode.OrderItemsEmpty, ex.Code);
    }

    [Fact]
    public async Task 新增销售订单_客户不存在_应报NotFound()
    {
        var (context, _, _, _, handler, _) = CreateHandler();
        var p1 = await SeedProductAsync(context, "sku-x");

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(Guid.NewGuid(), (p1.Id, 1, 1m))));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 新增销售订单_客户停用_应报PartnerDisabled()
    {
        var (context, _, _, _, handler, _) = CreateHandler();
        var partner = TestSupport.NewPartner("停用客户", type: PartnerType.Customer, status: PartnerStatus.Disabled);
        context.Partners.Add(partner);
        var p1 = await SeedProductAsync(context, "sku-off-c");
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(partner.Id, (p1.Id, 1, 1m))));
        Assert.Equal(ErrorCode.PartnerDisabled, ex.Code);
    }

    [Fact]
    public async Task 新增销售订单_纯供应商下单_应报PartnerTypeMismatch()
    {
        var (context, _, _, _, handler, _) = CreateHandler();
        var partner = TestSupport.NewPartner("纯供应商", type: PartnerType.Supplier);
        context.Partners.Add(partner);
        var p1 = await SeedProductAsync(context, "sku-supp");
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(partner.Id, (p1.Id, 1, 1m))));
        Assert.Equal(ErrorCode.PartnerTypeMismatch, ex.Code);
    }

    [Fact]
    public async Task 新增销售订单_商品停用_应报ProductDisabled()
    {
        var (context, _, _, _, handler, _) = CreateHandler();
        var partner = TestSupport.NewPartner("客户", type: PartnerType.Customer);
        context.Partners.Add(partner);
        var p1 = await SeedProductAsync(context, "sku-off", status: ProductStatus.Disabled);
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(partner.Id, (p1.Id, 1, 1m))));
        Assert.Equal(ErrorCode.ProductDisabled, ex.Code);
    }

    // ============================== 事务失败回滚 ==============================

    [Fact]
    public async Task 新增销售订单_Add失败_应回滚且不提交()
    {
        var (context, _, orders, _, handler, calls) = CreateHandler();
        var (partner, p1, _) = await SeedAsync(context);
        orders.AddFailure = () => new InvalidOperationException("模拟数据库写入失败");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 1, 1m))));
        Assert.Contains("模拟数据库写入失败", ex.Message);

        Assert.Equal(new[] { "Begin", "Generate", "Add", "Rollback" }, calls.ToArray());
        Assert.DoesNotContain("Commit", calls);
    }

    [Fact]
    public async Task 新增销售订单_Commit失败_应回滚且异常上抛()
    {
        var (context, _, _, uow, handler, calls) = CreateHandler();
        var (partner, p1, _) = await SeedAsync(context);
        uow.CommitFailure = () => new InvalidOperationException("模拟提交失败");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 1, 1m))));
        Assert.Contains("模拟提交失败", ex.Message);

        Assert.Equal(new[] { "Begin", "Generate", "Add", "Commit", "Rollback" }, calls.ToArray());
    }
}
