using App.Core.Abstractions;
using App.Core.Features.Reports;
using App.Core.Features.Reports.GetCostProfitReport;

namespace App.Tests;

/// <summary>
/// 成本与毛利报表用例测试（specs/026-erp-cost design.md §6）：
/// 传参透传、毛利 / 毛利率计算（收入为 0 时为 null）、成本不完整标记、合计映射。
/// </summary>
public class GetCostProfitReportRequestHandlerTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset End = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static CostProfitItem Item(string name, int quantity, decimal salesAmount, decimal costAmount, bool missing = false)
        => new()
        {
            Key = Guid.NewGuid(),
            Name = name,
            SalesQuantity = quantity,
            SalesAmount = salesAmount,
            CostAmount = costAmount,
            HasMissingCost = missing,
        };

    [Fact]
    public async Task 查询_应把期间与筛选分页条件原样传给仓储()
    {
        var repository = new FakeReportQueryRepository();
        var handler = new GetCostProfitReportRequestHandler(repository);
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        await handler.HandleAsync(new GetCostProfitReportRequest
        {
            Start = Start,
            End = End,
            ProductId = productId,
            CategoryId = categoryId,
            GroupBy = CostProfitGroupBy.Partner,
            Page = 2,
            PageSize = 50,
        });

        var args = repository.LastCostProfitArgs;
        Assert.NotNull(args);
        Assert.Equal(Start, args.Value.Start);
        Assert.Equal(End, args.Value.End);
        Assert.Equal(productId, args.Value.ProductId);
        Assert.Equal(categoryId, args.Value.CategoryId);
        Assert.Equal(CostProfitGroupBy.Partner, args.Value.GroupBy);
        Assert.Equal(2, args.Value.Page);
        Assert.Equal(50, args.Value.PageSize);
    }

    [Fact]
    public async Task 毛利_应为收入减成本_毛利率按收入占比且收入为0时为null()
    {
        var repository = new FakeReportQueryRepository
        {
            CostProfitItems =
            [
                Item("GI202609010001", 5, 150m, 75m),
                Item("无收入单据", 0, 0m, 0m),
            ],
            CostProfitTotal = 2,
        };
        var handler = new GetCostProfitReportRequestHandler(repository);

        var result = await handler.HandleAsync(new GetCostProfitReportRequest { Start = Start, End = End });

        var first = result.Items[0];
        Assert.Equal(75m, first.GrossProfit);
        Assert.Equal(0.5m, first.GrossProfitRate);

        // 销售收入为 0 → 毛利率为 null（前端显示 `-`），不是 0 也不是 NaN
        Assert.Null(result.Items[1].GrossProfitRate);
    }

    [Fact]
    public async Task 成本不完整_应按标记透传且合计口径与当前页无关()
    {
        var repository = new FakeReportQueryRepository
        {
            CostProfitItems = [Item("GI202609010001", 5, 150m, 0m, missing: true)],
            CostProfitTotal = 7,
            CostProfitSummary = new CostProfitTotal
            {
                SalesQuantity = 35,
                SalesAmount = 1050m,
                CostAmount = 600m,
                HasMissingCost = true,
            },
        };
        var handler = new GetCostProfitReportRequestHandler(repository);

        var result = await handler.HandleAsync(new GetCostProfitReportRequest { Start = Start, End = End });

        Assert.True(Assert.Single(result.Items).HasMissingCost);
        Assert.Equal(7, result.Total);
        Assert.Equal(35, result.Summary.SalesQuantity);
        Assert.Equal(1050m, result.Summary.SalesAmount);
        Assert.Equal(600m, result.Summary.CostAmount);
        Assert.Equal(450m, result.Summary.GrossProfit);
        Assert.True(result.Summary.HasMissingCost);
    }

    [Fact]
    public async Task 退货冲减_退货行应为负收入与正成本()
    {
        var repository = new FakeReportQueryRepository
        {
            // 退货单：收入 −30、成本 +15（退货入库流水成本为正，天然冲减成本）
            CostProfitItems = [Item("SR202609010001", -1, -30m, 15m)],
            CostProfitTotal = 1,
        };
        var handler = new GetCostProfitReportRequestHandler(repository);

        var result = await handler.HandleAsync(new GetCostProfitReportRequest { Start = Start, End = End });

        var item = Assert.Single(result.Items);
        Assert.Equal(-1, item.SalesQuantity);
        Assert.Equal(-30m, item.SalesAmount);
        Assert.Equal(15m, item.CostAmount);
        Assert.Equal(-45m, item.GrossProfit);
    }
}
