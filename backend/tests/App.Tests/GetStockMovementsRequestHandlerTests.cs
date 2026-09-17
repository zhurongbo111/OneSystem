using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Features.StockMovements.GetStockMovements;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// GetStockMovements 用例测试（design.md §6）：
/// Handler 用行为型假仓储验证筛选传参 / 符号与空值透传 / 分页 total；
/// 真实仓储 + InMemory 验证商品 / 操作人联查与时间倒序。
/// </summary>
public class GetStockMovementsRequestHandlerTests
{
    private static readonly DateTimeOffset Time = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static StockMovementItem Item(StockMovementType type, int quantity, string? sourceNo, Guid? createdBy) => new()
    {
        Id = Guid.NewGuid(),
        ProductId = Guid.NewGuid(),
        ProductCode = "sku-mv",
        ProductName = "流水商品",
        Unit = "个",
        MovementType = type,
        Quantity = quantity,
        SourceNo = sourceNo,
        Remark = null,
        CreatedAt = Time,
        CreatedByName = createdBy is null ? null : "管理员",
    };

    /// <summary>
    /// 假仓储：记录最近一次 GetPagedAsync 入参；返回 2 行（带值 / 空值各一）
    /// </summary>
    private sealed class FakeMovementRepository : IStockMovementRepository
    {
        public (string? Keyword, Guid? ProductId, StockMovementType? Type,
            DateTimeOffset? Start, DateTimeOffset? End, int Page, int PageSize) LastRequest { get; private set; }

        public Task AppendAsync(StockMovement movement, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<(IReadOnlyList<StockMovementItem> Items, int Total)> GetPagedAsync(
            string? keyword, Guid? productId, StockMovementType? type,
            DateTimeOffset? start, DateTimeOffset? end,
            int page, int pageSize, CancellationToken cancellationToken = default)
        {
            LastRequest = (keyword, productId, type, start, end, page, pageSize);
            // 两行：入库 +5（有单号 / 操作人）；出库 -3（无单号 / 无操作人，验证空值透传）
            return Task.FromResult<(IReadOnlyList<StockMovementItem>, int)>(
                (new[]
                {
                    Item(StockMovementType.PurchaseInbound, 5, "GR202601010001", Guid.NewGuid()),
                    Item(StockMovementType.SalesOutbound, -3, null, null),
                }, 41));
        }

        public Task<int> SumQuantityAsync(Guid productId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<Guid>> GetProductIdsWithMovementsAsync(
            IReadOnlyList<Guid> productIds, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    // ============================== Handler：传参 / 映射 / 分页 ==============================

    [Fact]
    public async Task 筛选条件_应原样透传给仓储且符号与空值透传()
    {
        var repo = new FakeMovementRepository();
        var handler = new GetStockMovementsRequestHandler(repo);
        var productId = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);

        var result = await handler.HandleAsync(new GetStockMovementsRequest
        {
            Keyword = "GR2026",
            ProductId = productId,
            Type = StockMovementType.SalesOutbound,
            Start = start,
            End = end,
            Page = 3,
            PageSize = 50,
        });

        Assert.Equal(("GR2026", productId, StockMovementType.SalesOutbound, start, end, 3, 50),
            (repo.LastRequest.Keyword, repo.LastRequest.ProductId, repo.LastRequest.Type,
             repo.LastRequest.Start, repo.LastRequest.End, repo.LastRequest.Page, repo.LastRequest.PageSize));

        // DTO 映射：符号保留（-3 为出库）
        Assert.Contains(result.Items, i => i.MovementType == StockMovementType.SalesOutbound && i.Quantity == -3);
        // 空值透传：无来源单号 / 无操作人 → null（前端显示 -）
        Assert.Contains(result.Items, i => i.SourceNo is null && i.Remark is null);
        // 分页 total 透传
        Assert.Equal(41, result.Total);
        Assert.Equal(3, result.Page);
        Assert.Equal(50, result.PageSize);
    }

    // ============================== 真实仓储 + InMemory：联查与排序 ==============================

    [Fact]
    public async Task 真实仓储_应联查商品与操作人且按变动时间倒序()
    {
        var context = TestSupport.CreateDbContext();
        var product = TestSupport.NewProduct("sku-mv-real", "联查商品");
        var admin = TestSupport.SeedAdmin(context);
        context.Products.Add(product);
        context.SaveChanges();

        var repo = new StockMovementRepository(context);
        await repo.AppendAsync(new StockMovement
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            MovementType = StockMovementType.PurchaseInbound,
            Quantity = 1,
            SourceNo = "GR202601010001",
            CreatedAt = Time.AddMinutes(-5),
            CreatedBy = admin.Id,
        });
        await repo.AppendAsync(new StockMovement
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            MovementType = StockMovementType.SalesOutbound,
            Quantity = -2,
            SourceNo = "GI202601010001",
            CreatedAt = Time,
            CreatedBy = null, // 系统操作：操作人姓名为 null
        });
        await context.SaveChangesAsync();

        var handler = new GetStockMovementsRequestHandler(repo);
        var result = await handler.HandleAsync(new GetStockMovementsRequest());

        Assert.Equal(2, result.Total);
        // 倒序：出库（Time）在前，入库（Time-5min）在后
        Assert.Equal(StockMovementType.SalesOutbound, result.Items[0].MovementType);
        Assert.Equal(StockMovementType.PurchaseInbound, result.Items[1].MovementType);

        // 联查带出商品编码 / 名称 / 单位
        Assert.All(result.Items, i =>
        {
            Assert.Equal("sku-mv-real", i.ProductCode);
            Assert.Equal("联查商品", i.ProductName);
            Assert.Equal("个", i.Unit);
        });

        // 联查操作人姓名：有 CreatedBy → 管理员；系统操作 → null
        Assert.Equal("管理员", result.Items[1].CreatedByName);
        Assert.Null(result.Items[0].CreatedByName);
    }

    [Fact]
    public async Task 真实仓储_商品与类型筛选_应命中对应流水()
    {
        var context = TestSupport.CreateDbContext();
        var p1 = TestSupport.NewProduct("sku-filter-1");
        var p2 = TestSupport.NewProduct("sku-filter-2");
        context.Products.AddRange(p1, p2);
        context.SaveChanges();

        var repo = new StockMovementRepository(context);
        await repo.AppendAsync(new StockMovement
        {
            Id = Guid.NewGuid(),
            ProductId = p1.Id,
            MovementType = StockMovementType.PurchaseInbound,
            Quantity = 1,
            CreatedAt = Time,
        });
        await repo.AppendAsync(new StockMovement
        {
            Id = Guid.NewGuid(),
            ProductId = p2.Id,
            MovementType = StockMovementType.SalesOutbound,
            Quantity = -2,
            CreatedAt = Time,
        });
        await context.SaveChangesAsync();

        var handler = new GetStockMovementsRequestHandler(repo);

        var byProduct = await handler.HandleAsync(new GetStockMovementsRequest { ProductId = p1.Id });
        Assert.Single(byProduct.Items);
        Assert.Equal(p1.Id, Guid.Parse(byProduct.Items[0].ProductId));

        var byType = await handler.HandleAsync(new GetStockMovementsRequest { Type = StockMovementType.SalesOutbound });
        Assert.Single(byType.Items);
        Assert.Equal(StockMovementType.SalesOutbound, byType.Items[0].MovementType);
    }
}
