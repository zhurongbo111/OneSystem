using App.Core.Abstractions;
using App.Core.Features.Reports.GetStockBalance;

namespace App.Tests;

/// <summary>
/// 库存余额表查询用例测试：筛选 / 分页透传、分类聚合字段映射、占比计算、全量合计。
/// </summary>
public class GetStockBalanceRequestHandlerTests
{
    private static StockBalanceItem NewBalanceItem(string categoryName, int productCount, int totalQuantity, int zero, int belowSafety)
        => new()
        {
            CategoryId = Guid.NewGuid(),
            CategoryName = categoryName,
            ProductCount = productCount,
            TotalQuantity = totalQuantity,
            ZeroStockCount = zero,
            BelowSafetyCount = belowSafety,
        };

    [Fact]
    public async Task 查询_应把关键词与分页条件原样传给仓储()
    {
        var repository = new FakeReportQueryRepository();
        var handler = new GetStockBalanceRequestHandler(repository);
        var categoryId = Guid.NewGuid();

        await handler.HandleAsync(new GetStockBalanceRequest
        {
            Keyword = "sku",
            CategoryId = categoryId,
            Page = 3,
            PageSize = 10,
        });

        var args = repository.LastStockBalanceArgs;
        Assert.NotNull(args);
        Assert.Equal("sku", args.Value.Keyword);
        Assert.Equal(categoryId, args.Value.CategoryId);
        Assert.Equal(3, args.Value.Page);
        Assert.Equal(10, args.Value.PageSize);
    }

    [Fact]
    public async Task 查询_应映射分类聚合字段与全库合计()
    {
        var repository = new FakeReportQueryRepository
        {
            StockBalanceItems = [NewBalanceItem("分类一", productCount: 4, totalQuantity: 30, zero: 1, belowSafety: 2)],
            StockBalanceTotal = 1,
            StockBalanceSummary = new StockBalanceTotal
            {
                ProductCount = 4,
                TotalQuantity = 100,
                ZeroStockCount = 1,
                BelowSafetyCount = 2,
            },
        };
        var handler = new GetStockBalanceRequestHandler(repository);

        var result = await handler.HandleAsync(new GetStockBalanceRequest());

        var item = Assert.Single(result.Items);
        Assert.Equal("分类一", item.CategoryName);
        Assert.Equal(4, item.ProductCount);
        Assert.Equal(30, item.TotalQuantity);
        Assert.Equal(1, item.ZeroStockCount);
        Assert.Equal(2, item.BelowSafetyCount);
        // 占比分母为全量筛选库存总量（100），不是当前页合计
        Assert.Equal(0.3m, item.QuantityRatio);
    }

    [Fact]
    public async Task 占比_全量库存为0时应按0处理_不产生除零()
    {
        var repository = new FakeReportQueryRepository
        {
            StockBalanceItems = [NewBalanceItem("分类一", 1, 0, 1, 0)],
            StockBalanceTotal = 1,
            StockBalanceSummary = new StockBalanceTotal
            {
                ProductCount = 1,
                TotalQuantity = 0,
                ZeroStockCount = 1,
                BelowSafetyCount = 0,
            },
        };
        var handler = new GetStockBalanceRequestHandler(repository);

        var result = await handler.HandleAsync(new GetStockBalanceRequest());

        Assert.Equal(0m, Assert.Single(result.Items).QuantityRatio);
    }
}
