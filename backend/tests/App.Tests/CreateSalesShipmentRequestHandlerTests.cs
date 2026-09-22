using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.SalesShipments.CreateSalesShipment;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// CreateSalesShipmentRequestHandler 测试（design.md §6）：
/// 成功路径断言单号 + 后端重算 + 明细快照 + 逐行 TryDecrementAsync(qty) 先于 Add + Commit；
/// 库存不足 → 40103（message 含首个不足商品名，Rollback 且 Add 未被调用）；
/// 错误码 40110（明细空）/ 40400（客户、商品不存在）/ 40108（客户停用）/ 40109（类型不匹配）/ 40107（商品停用）；
/// 事务调用序列 Begin→TryDecrement→Generate→Add→Commit；Add 失败 / Commit 失败 → Rollback。
/// 客户 / 商品用真实仓储（纯查询 InMemory 可用）；销售单 / 库存 / 工作单元用行为型假实现（规避 InMemory 不支持 ExecuteUpdateAsync）。
/// </summary>
public class CreateSalesShipmentRequestHandlerTests
{
    private static readonly DateTimeOffset OrderDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (AppDbContext Context, StubCurrentUser User, FakeSalesShipmentRepository Orders,
        FakeInventoryRepository Inventory, FakeStockMovementRepository Movements, RecordingUnitOfWork Uow,
        CreateSalesShipmentRequestHandler Handler, List<string> Calls) CreateHandler()
    {
        var context = TestSupport.CreateDbContext();
        var user = new StubCurrentUser(Guid.NewGuid());
        var calls = new List<string>();
        var orders = new FakeSalesShipmentRepository(calls);
        var inventory = new FakeInventoryRepository(calls);
        var movements = new FakeStockMovementRepository(calls);
        var uow = new RecordingUnitOfWork(calls);
        var handler = new CreateSalesShipmentRequestHandler(
            orders,
            new FakeSalesOrderRepository(calls),
            new PartnerRepository(context),
            new ProductRepository(context),
            inventory,
            movements,
            uow,
            user, TestSupport.AuditLogger);
        return (context, user, orders, inventory, movements, uow, handler, calls);
    }

    private static CreateSalesShipmentRequest RequestWith(Guid partnerId, params (Guid ProductId, int Qty, decimal Price)[] lines)
        => new()
        {
            PartnerId = partnerId,
            OrderDate = OrderDate,
            Items = lines.Select(l => new CreateSalesShipmentItem { ProductId = l.ProductId, Quantity = l.Qty, UnitPrice = l.Price }).ToList(),
        };

    private static async Task<(Partner Partner, Product P1, Product P2)> SeedAsync(AppDbContext context, int stock1 = 10, int stock2 = 8, FakeInventoryRepository? inventory = null)
    {
        var partner = TestSupport.NewPartner("测试客户", type: PartnerType.Customer);
        context.Partners.Add(partner);
        var p1 = TestSupport.NewProduct("sku-so-1", "商品一");
        var p2 = TestSupport.NewProduct("sku-so-2", "商品二");
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
    public async Task 新增销售单_成功_应生成单号并重算小计总额且逐行库存减少()
    {
        var (context, user, orders, inventory, movements, _, handler, _) = CreateHandler();
        var (partner, p1, p2) = await SeedAsync(context, inventory: inventory);

        var result = await handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 3, 1.5m), (p2.Id, 2, 10m)));

        // 单号：GI + yyyyMMdd + 4 位序号
        Assert.Matches("^GI20260101\\d{4}$", result.ShipmentNo);
        Assert.Equal(0m, result.SettledAmount);
        Assert.Equal(24.5m, result.UnsettledAmount);
        Assert.Equal((int)SettlementState.Unsettled, result.SettlementState);
        Assert.Equal((int)OrderStatus.Normal, result.Status);
        Assert.Equal(partner.Name, result.PartnerName);

        // 后端重算：小计 = 数量 × 单价，总额 = Σ 小计（不信任前端传值）
        Assert.Equal(4.5m, result.Items[0].Subtotal);   // 3 × 1.5
        Assert.Equal(20m, result.Items[1].Subtotal);     // 2 × 10
        Assert.Equal(24.5m, result.TotalAmount);

        // 明细快照取自商品档案（名称 / 单位）
        Assert.Equal(p1.Name, result.Items[0].ProductName);
        Assert.Equal(p1.Unit, result.Items[0].Unit);

        // 逐行扣减 = 数量（调用顺序与明细一致）
        Assert.Equal(
            new[] { (p1.Id, 3), (p2.Id, 2) },
            inventory.Decrements.Select(i => (i.ProductId, i.Amount)).ToArray());
        Assert.Equal(7, inventory.GetQuantity(p1.Id));   // 10 - 3
        Assert.Equal(6, inventory.GetQuantity(p2.Id));   // 8 - 2

        // 逐行追加销售出库流水：类型 / 负方向 / 来源 / 操作人
        Assert.Equal(2, movements.Appended.Count);
        Assert.All(movements.Appended, m =>
        {
            Assert.Equal(StockMovementType.SalesOutbound, m.MovementType);
            Assert.Equal(user.UserId, m.CreatedBy);
            Assert.Equal(Guid.Parse(result.Id), m.SourceId);
            Assert.Equal(result.ShipmentNo, m.SourceNo);
        });
        Assert.Equal(new[] { (p1.Id, -3), (p2.Id, -2) }, movements.Appended.Select(m => (m.ProductId, m.Quantity)).ToArray());

        // 对账一致性：Σ 流水变动量 == 库存净变动量（种子库存不入流水，故为负值而非终值）
        Assert.Equal(-3, await movements.SumQuantityAsync(p1.Id));
        Assert.Equal(-2, await movements.SumQuantityAsync(p2.Id));
    }

    [Fact]
    public async Task 新增销售单_当天已有单据_单号序号应递增()
    {
        var (context, _, _, inventory, _, _, handler, _) = CreateHandler();
        var (partner, p1, _) = await SeedAsync(context, inventory: inventory);

        var first = await handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 1, 1m)));
        var second = await handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 1, 1m)));

        Assert.Matches("^GI20260101\\d{4}$", first.ShipmentNo);
        Assert.Matches("^GI20260101\\d{4}$", second.ShipmentNo);
        Assert.NotEqual(first.ShipmentNo, second.ShipmentNo);
        Assert.True(
            int.Parse(second.ShipmentNo[^4..]) > int.Parse(first.ShipmentNo[^4..]),
            $"单号序号应递增：{first.ShipmentNo} → {second.ShipmentNo}");
    }

    [Fact]
    public async Task 新增销售单_成功_事务调用顺序应为Begin_TryDecrement_Generate_Add_Commit()
    {
        var (context, _, _, inventory, _, _, handler, calls) = CreateHandler();
        var (partner, p1, _) = await SeedAsync(context, inventory: inventory);

        await handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 1, 1m)));

        // 销售先扣库存、成功后再插单、写流水（design.md §3.4）
        // 成本：销售出库按「变动前」均价结转 → 扣减前先 GetAverageCost，写流水前 ApplyOutboundCost
        Assert.Equal(new[] { "Begin", "GetAverageCost", "TryDecrement", "Generate", "Add", "ApplyOutboundCost", "Append", "Commit" }, calls.ToArray());
    }

    // ============================== 库存不足 ==============================

    [Fact]
    public async Task 新增销售单_库存不足_应报InsufficientStock且回滚整单()
    {
        var (context, _, _, inventory, movements, uow, handler, calls) = CreateHandler();
        var (partner, p1, _) = await SeedAsync(context, stock1: 5, stock2: 8, inventory: inventory);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(partner.Id, (p1.Id, 10, 1m)))); // 10 > 当前库存 5

        Assert.Equal(ErrorCode.InsufficientStock, ex.Code);
        Assert.Contains("商品一", ex.Message);
        Assert.Contains("5", ex.Message);

        // 回滚整单：Rollback 被调用、Add 未被调用（单据未插入）、不写流水
        Assert.Contains("Rollback", calls);
        Assert.DoesNotContain("Add", calls);
        Assert.DoesNotContain("Commit", calls);
        Assert.Empty(inventory.Decrements);
        Assert.Empty(movements.Appended);
        Assert.Equal(5, inventory.GetQuantity(p1.Id)); // 库存未变动
    }

    [Fact]
    public async Task 新增销售单_第二行库存不足_应报InsufficientStock且首行扣减一并回滚()
    {
        var (context, _, _, inventory, movements, _, handler, calls) = CreateHandler();
        var (partner, p1, p2) = await SeedAsync(context, stock1: 10, stock2: 1, inventory: inventory);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(partner.Id, (p1.Id, 2, 1m), (p2.Id, 3, 1m)))); // 第二行 3 > 1

        Assert.Equal(ErrorCode.InsufficientStock, ex.Code);
        Assert.Contains("商品二", ex.Message);

        // 假实现无真实事务：断言未提交且未插单、不写流水（真实 PostgreSQL 事务内首行扣减随 Rollback 恢复）
        Assert.DoesNotContain("Add", calls);
        Assert.DoesNotContain("Commit", calls);
        Assert.Contains("Rollback", calls);
        Assert.Empty(movements.Appended);
    }

    // ============================== 客户校验 ==============================

    [Fact]
    public async Task 新增销售单_客户不存在_应报NotFound()
    {
        var (context, _, _, inventory, _, _, handler, _) = CreateHandler();
        var p1 = await SeedProductAsync(context, "sku-x");
        inventory.Seed(p1.Id, 10);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(Guid.NewGuid(), (p1.Id, 1, 1m))));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 新增销售单_客户停用_应报PartnerDisabled()
    {
        var (context, _, _, inventory, _, _, handler, _) = CreateHandler();
        var partner = TestSupport.NewPartner("停用客户", type: PartnerType.Customer, status: PartnerStatus.Disabled);
        context.Partners.Add(partner);
        var p1 = await SeedProductAsync(context, "sku-off-p");
        await context.SaveChangesAsync();
        inventory.Seed(p1.Id, 10);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(partner.Id, (p1.Id, 1, 1m))));
        Assert.Equal(ErrorCode.PartnerDisabled, ex.Code);
    }

    [Fact]
    public async Task 新增销售单_纯供应商开销售单_应报PartnerTypeMismatch()
    {
        var (context, _, _, inventory, _, _, handler, _) = CreateHandler();
        var partner = TestSupport.NewPartner("纯供应商", type: PartnerType.Supplier);
        context.Partners.Add(partner);
        var p1 = await SeedProductAsync(context, "sku-sup");
        await context.SaveChangesAsync();
        inventory.Seed(p1.Id, 10);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(partner.Id, (p1.Id, 1, 1m))));
        Assert.Equal(ErrorCode.PartnerTypeMismatch, ex.Code);
    }

    [Fact]
    public async Task 新增销售单_两者类型_应通过类型校验()
    {
        var (context, _, _, inventory, _, _, handler, _) = CreateHandler();
        var partner = TestSupport.NewPartner("两者单位", type: PartnerType.Both);
        context.Partners.Add(partner);
        var p1 = await SeedProductAsync(context, "sku-both");
        await context.SaveChangesAsync();
        inventory.Seed(p1.Id, 10);

        var result = await handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 1, 1m)));
        Assert.Equal((int)OrderStatus.Normal, result.Status);
    }

    // ============================== 商品校验 ==============================

    [Fact]
    public async Task 新增销售单_商品不存在_应报NotFound()
    {
        var (context, _, _, inventory, _, _, handler, _) = CreateHandler();
        var partner = TestSupport.NewPartner("客户", type: PartnerType.Customer);
        context.Partners.Add(partner);
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(partner.Id, (Guid.NewGuid(), 1, 1m))));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 新增销售单_商品停用_应报ProductDisabled()
    {
        var (context, _, _, inventory, _, _, handler, _) = CreateHandler();
        var partner = TestSupport.NewPartner("客户", type: PartnerType.Customer);
        context.Partners.Add(partner);
        var p1 = await SeedProductAsync(context, "sku-off", status: ProductStatus.Disabled);
        await context.SaveChangesAsync();
        inventory.Seed(p1.Id, 10);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(partner.Id, (p1.Id, 1, 1m))));
        Assert.Equal(ErrorCode.ProductDisabled, ex.Code);
    }

    [Fact]
    public async Task 新增销售单_明细为空_应报OrderItemsEmpty()
    {
        var (context, _, _, inventory, _, _, handler, _) = CreateHandler();
        var partner = TestSupport.NewPartner("客户", type: PartnerType.Customer);
        context.Partners.Add(partner);
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateSalesShipmentRequest
        {
            PartnerId = partner.Id,
            OrderDate = OrderDate,
            Items = Array.Empty<CreateSalesShipmentItem>(),
        }));
        Assert.Equal(ErrorCode.OrderItemsEmpty, ex.Code);
    }

    // ============================== 事务失败回滚 ==============================

    [Fact]
    public async Task 新增销售单_Add失败_应回滚且不提交()
    {
        var (context, _, orders, inventory, movements, uow, handler, calls) = CreateHandler();
        var (partner, p1, _) = await SeedAsync(context, inventory: inventory);
        orders.AddFailure = () => new InvalidOperationException("模拟数据库写入失败");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 1, 1m))));
        Assert.Contains("模拟数据库写入失败", ex.Message);

        // Begin → TryDecrement → Generate → Add → Rollback，且从未 Commit；Add 失败不写流水
        Assert.Equal(new[] { "Begin", "GetAverageCost", "TryDecrement", "Generate", "Add", "Rollback" }, calls.ToArray());
        Assert.DoesNotContain("Commit", calls);
        Assert.Empty(movements.Appended);
    }

    [Fact]
    public async Task 新增销售单_Commit失败_应回滚且异常上抛()
    {
        var (context, _, _, inventory, _, uow, handler, calls) = CreateHandler();
        var (partner, p1, _) = await SeedAsync(context, inventory: inventory);
        uow.CommitFailure = () => new InvalidOperationException("模拟提交失败");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(RequestWith(partner.Id, (p1.Id, 1, 1m))));
        Assert.Contains("模拟提交失败", ex.Message);

        // Begin → TryDecrement → Generate → Add → Append → Commit(抛) → Rollback，异常上抛
        Assert.Equal(new[] { "Begin", "GetAverageCost", "TryDecrement", "Generate", "Add", "ApplyOutboundCost", "Append", "Commit", "Rollback" }, calls.ToArray());
    }
}
