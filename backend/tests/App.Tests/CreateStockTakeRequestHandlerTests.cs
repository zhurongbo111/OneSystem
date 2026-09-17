using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.StockTakes.CreateStockTake;

using Xunit;

namespace App.Tests;

/// <summary>
/// CreateStockTakeRequestHandler 测试（design.md §6）：
/// 盘点成功（单号 ST+yyyyMMdd、快照、差异后端重算忽略前端、差异行 SetQuantityAsync + AppendAsync(StockTakeAdjust)、
/// 无差异行不写、Commit）/ 期初成功（InitialStock 流水）/ 期初限制 40111（不写库存流水不落单）/
/// 异常 40110（明细空）40400（商品不存在）40107（商品停用）/ 事务 Commit 失败回滚 / 单号冲突重试。
/// 商品 / 盘点单 / 库存 / 流水均用行为型假实现（规避 InMemory 不支持 ExecuteUpdateAsync，见 design.md §6）。
/// </summary>
public class CreateStockTakeRequestHandlerTests
{
    private static readonly DateTimeOffset TakeDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (FakeProductRepository Products, FakeStockTakeRepository Takes, FakeInventoryRepository Inventory,
        FakeStockMovementRepository Movements, RecordingUnitOfWork Uow, CreateStockTakeRequestHandler Handler,
        List<string> Calls) CreateHandler()
    {
        var products = new FakeProductRepository();
        var calls = new List<string>();
        var takes = new FakeStockTakeRepository(calls);
        var inventory = new FakeInventoryRepository(calls);
        var movements = new FakeStockMovementRepository(calls);
        var uow = new RecordingUnitOfWork(calls);
        var handler = new CreateStockTakeRequestHandler(
            takes,
            products,
            inventory,
            movements,
            uow,
            new StubCurrentUser(Guid.NewGuid()));
        return (products, takes, inventory, movements, uow, handler, calls);
    }

    private static Product SeedProduct(FakeProductRepository products, string code, string unit = "个",
        ProductStatus status = ProductStatus.Enabled)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = $"商品-{code}",
            Unit = unit,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        products.ById[product.Id] = product;
        return product;
    }

    private static CreateStockTakeRequest RequestWith(StockTakeType type, params (Guid ProductId, int Actual)[] lines)
        => new()
        {
            Type = type,
            TakeDate = TakeDate,
            Items = lines.Select(l => new CreateStockTakeItem { ProductId = l.ProductId, ActualQuantity = l.Actual }).ToList(),
        };

    // ============================== 成功路径（盘点）==============================

    [Fact]
    public async Task 新增盘点_成功_应生成ST单号重算差异且仅差异行改库存写流水()
    {
        var (products, _, inventory, movements, uow, handler, calls) = CreateHandler();
        var a = SeedProduct(products, "st-a", "箱");
        var b = SeedProduct(products, "st-b");
        inventory.Seed(a.Id, 5);   // 账面 5，实盘 7 → 差异 +2
        // b 无库存行，账面视为 0，实盘 3 → 差异 +3

        var result = await handler.HandleAsync(RequestWith(StockTakeType.Take, (a.Id, 7), (b.Id, 3)));

        // 单号：ST + yyyyMMdd + 4 位序号
        Assert.Matches("^ST20260101\\d{4}$", result.TakeNo);
        Assert.Equal((int)StockTakeType.Take, result.Type);
        Assert.Equal(2, result.ItemCount);
        Assert.Equal(2, result.DiffItemCount);

        // 明细快照（编码 / 名称 / 单位取自商品档案）+ 差异后端重算
        Assert.Equal("st-a", result.Items[0].ProductCode);
        Assert.Equal("商品-st-a", result.Items[0].ProductName);
        Assert.Equal("箱", result.Items[0].Unit);
        Assert.Equal(5, result.Items[0].BookQuantity);
        Assert.Equal(7, result.Items[0].ActualQuantity);
        Assert.Equal(2, result.Items[0].Difference);
        Assert.Equal(3, result.Items[1].Difference);

        // 仅差异行改库存（SetQuantityAsync 按实盘设定），且两行均差异 → 两条
        Assert.Equal(new[] { (a.Id, 7), (b.Id, 3) }, inventory.Sets.Select(s => s).ToArray());
        Assert.Equal(7, inventory.GetQuantity(a.Id));
        Assert.Equal(3, inventory.GetQuantity(b.Id));

        // 仅差异行写流水：类型 / 变动量 = 差异（带符号）/ 来源指向盘点单
        Assert.Equal(2, movements.Appended.Count);
        Assert.All(movements.Appended, m =>
        {
            Assert.Equal(StockMovementType.StockTakeAdjust, m.MovementType);
            Assert.Equal(result.TakeNo, m.SourceNo);
        });
        Assert.Equal(new[] { (a.Id, 2), (b.Id, 3) }, movements.Appended.Select(m => (m.ProductId, m.Quantity)).ToArray());

        // 对账一致性：Σ 流水变动量 == 库存净变动（期初账面 a=5 / b=0 → 净变动 = 终值 − 账面）
        Assert.Equal(2, await movements.SumQuantityAsync(a.Id));
        Assert.Equal(3, await movements.SumQuantityAsync(b.Id));

        // 提交发生
        Assert.Contains("Commit", calls);
    }

    [Fact]
    public async Task 新增盘点_无差异行_只落明细不改库存不写流水()
    {
        var (products, _, inventory, movements, _, handler, _) = CreateHandler();
        var a = SeedProduct(products, "st-eq");
        inventory.Seed(a.Id, 8);   // 实盘 = 账面 8 → 差异 0

        var result = await handler.HandleAsync(RequestWith(StockTakeType.Take, (a.Id, 8)));

        Assert.Equal(1, result.ItemCount);
        Assert.Equal(0, result.DiffItemCount);
        Assert.Equal(0, result.Items[0].Difference);
        Assert.Empty(inventory.Sets);            // 无差异行不调用 SetQuantityAsync
        Assert.Empty(movements.Appended);        // 无差异行不写流水
    }

    [Fact]
    public async Task 新增盘点_当天已有单据_单号序号应递增()
    {
        var (products, _, _, _, _, handler, _) = CreateHandler();
        var a = SeedProduct(products, "st-seq");

        var first = await handler.HandleAsync(RequestWith(StockTakeType.Take, (a.Id, 1)));
        var second = await handler.HandleAsync(RequestWith(StockTakeType.Take, (a.Id, 1)));

        Assert.Matches("^ST20260101\\d{4}$", first.TakeNo);
        Assert.Matches("^ST20260101\\d{4}$", second.TakeNo);
        Assert.NotEqual(first.TakeNo, second.TakeNo);
        Assert.True(
            int.Parse(second.TakeNo[^4..]) > int.Parse(first.TakeNo[^4..]),
            $"单号序号应递增：{first.TakeNo} → {second.TakeNo}");
    }

    // ============================== 成功路径（期初建账）==============================

    [Fact]
    public async Task 新增期初建账_成功_流水类型应为InitialStock且无变动商品通过()
    {
        var (products, _, inventory, movements, _, handler, _) = CreateHandler();
        var a = SeedProduct(products, "st-init");
        // a 无任何库存变动（movements 为空）→ 期初限制通过

        var result = await handler.HandleAsync(RequestWith(StockTakeType.Initial, (a.Id, 5)));

        Assert.Equal((int)StockTakeType.Initial, result.Type);
        Assert.Equal(5, result.Items[0].Difference);  // 账面 0 → 实盘 5
        var movement = Assert.Single(movements.Appended);
        Assert.Equal(StockMovementType.InitialStock, movement.MovementType);
        Assert.Equal((a.Id, 5), (movement.ProductId, movement.Quantity));
        Assert.Equal(5, inventory.GetQuantity(a.Id));
    }

    // ============================== 校验 / 限制 ==============================

    [Fact]
    public async Task 新增盘点_明细为空_应报OrderItemsEmpty()
    {
        var (products, _, _, _, _, handler, _) = CreateHandler();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateStockTakeRequest
        {
            Type = StockTakeType.Take,
            TakeDate = TakeDate,
            Items = Array.Empty<CreateStockTakeItem>(),
        }));
        Assert.Equal(ErrorCode.OrderItemsEmpty, ex.Code);
    }

    [Fact]
    public async Task 新增盘点_商品不存在_应报NotFound()
    {
        var (products, _, _, _, _, handler, _) = CreateHandler();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(StockTakeType.Take, (Guid.NewGuid(), 1))));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 新增盘点_商品停用_应报ProductDisabled()
    {
        var (products, _, _, _, _, handler, _) = CreateHandler();
        var a = SeedProduct(products, "st-off", status: ProductStatus.Disabled);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(StockTakeType.Take, (a.Id, 1))));
        Assert.Equal(ErrorCode.ProductDisabled, ex.Code);
    }

    [Fact]
    public async Task 新增期初建账_已有库存变动商品_应报StockInitialNotAllowed且不写库存流水不落单()
    {
        var (products, takes, inventory, movements, _, handler, _) = CreateHandler();
        var a = SeedProduct(products, "st-busy");
        // a 已发生过库存变动（期初建账流水），不允许再建期初
        movements.Appended.Add(new StockMovement
        {
            Id = Guid.NewGuid(),
            ProductId = a.Id,
            MovementType = StockMovementType.InitialStock,
            Quantity = 2,
            SourceNo = "ST202601010001",
            CreatedAt = DateTimeOffset.UtcNow,
        });

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            RequestWith(StockTakeType.Initial, (a.Id, 5))));
        Assert.Equal(ErrorCode.StockInitialNotAllowed, ex.Code);

        // 快速失败：不写库存、不写流水（仅种子那条）、不落单
        Assert.Empty(inventory.Sets);
        Assert.Single(movements.Appended);
        Assert.Empty(takes.Takes);
    }

    // ============================== 事务 / 重试 ==============================

    [Fact]
    public async Task 新增盘点_单号冲突_应回滚重试并最终成功()
    {
        var (products, takes, _, _, uow, handler, _) = CreateHandler();
        var a = SeedProduct(products, "st-retry");
        takes.AddConflictCount = 1;   // 首次 AddAsync 抛单号冲突

        var result = await handler.HandleAsync(RequestWith(StockTakeType.Take, (a.Id, 1)));

        Assert.Matches("^ST20260101\\d{4}$", result.TakeNo);
        Assert.Equal(2, takes.GenerateCount);   // 重试重新生成单号
        Assert.Single(takes.Takes);             // 最终落单一条
    }

    [Fact]
    public async Task 新增盘点_Commit失败_应回滚且异常上抛()
    {
        var (products, _, _, _, uow, handler, calls) = CreateHandler();
        var a = SeedProduct(products, "st-cfail");
        uow.CommitFailure = () => new InvalidOperationException("模拟提交失败");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(RequestWith(StockTakeType.Take, (a.Id, 1))));
        Assert.Contains("模拟提交失败", ex.Message);

        // 提交失败 → 回滚（异常上抛前已 Rollback）
        Assert.Contains("Rollback", calls);
    }
}
