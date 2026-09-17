using App.Core.Abstractions;
using App.Core.Features.Reports;
using App.Core.Features.Reports.GetSalesSummary;

namespace App.Tests;

/// <summary>
/// 销售汇总查询用例测试：分组维度传参、净额 = 出库 − 退货、无退货分组、合计口径。
/// </summary>
public class GetSalesSummaryRequestHandlerTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static SalesSummaryItem NewSummaryItem(string name, int orderCount, int outboundQuantity, decimal outboundAmount, int returnQuantity, decimal returnAmount)
        => new()
        {
            Key = Guid.NewGuid(),
            Name = name,
            Unit = null,
            OrderCount = orderCount,
            OutboundQuantity = outboundQuantity,
            OutboundAmount = outboundAmount,
            ReturnQuantity = returnQuantity,
            ReturnAmount = returnAmount,
        };

    [Fact]
    public async Task 查询_默认按往来单位维度传参()
    {
        var repository = new FakeReportQueryRepository();
        var handler = new GetSalesSummaryRequestHandler(repository);
        var partnerId = Guid.NewGuid();

        await handler.HandleAsync(new GetSalesSummaryRequest
        {
            Start = Start,
            End = End,
            PartnerId = partnerId,
            Page = 2,
            PageSize = 50,
        });

        var args = repository.LastSalesSummaryArgs;
        Assert.NotNull(args);
        Assert.Equal(Start, args.Value.Start);
        Assert.Equal(End, args.Value.End);
        Assert.Equal(partnerId, args.Value.PartnerId);
        Assert.False(args.Value.GroupByProduct);
        Assert.Equal(2, args.Value.Page);
        Assert.Equal(50, args.Value.PageSize);
    }

    [Fact]
    public async Task 查询_按商品维度时应传递商品分组标记()
    {
        var repository = new FakeReportQueryRepository();
        var handler = new GetSalesSummaryRequestHandler(repository);

        await handler.HandleAsync(new GetSalesSummaryRequest
        {
            Start = Start,
            End = End,
            GroupBy = SummaryGroupBy.Product,
        });

        var args = repository.LastSalesSummaryArgs;
        Assert.NotNull(args);
        Assert.True(args.Value.GroupByProduct);
    }

    [Fact]
    public async Task 查询_净额应为出库减退货且合计同步()
    {
        var repository = new FakeReportQueryRepository
        {
            SalesSummaryItems =
            [
                new SalesSummaryItem
                {
                    Key = Guid.NewGuid(),
                    Name = "商品一",
                    Unit = "个",
                    OrderCount = 2,
                    OutboundQuantity = 30,
                    OutboundAmount = 900m,
                    ReturnQuantity = 5,
                    ReturnAmount = 150m,
                },
            ],
            SalesSummaryTotal = 1,
            SalesSummarySummary = new SalesSummaryTotal
            {
                OrderCount = 2,
                OutboundQuantity = 30,
                OutboundAmount = 900m,
                ReturnQuantity = 5,
                ReturnAmount = 150m,
            },
        };
        var handler = new GetSalesSummaryRequestHandler(repository);

        var result = await handler.HandleAsync(new GetSalesSummaryRequest { Start = Start, End = End });

        var item = Assert.Single(result.Items);
        Assert.Equal("个", item.Unit);
        Assert.Equal(25, item.NetQuantity);
        Assert.Equal(750m, item.NetAmount);
        Assert.Equal(25, result.Summary.NetQuantity);
        Assert.Equal(750m, result.Summary.NetAmount);
    }

    [Fact]
    public async Task 查询_无退货分组退货列应为零且净额等于出库()
    {
        var repository = new FakeReportQueryRepository
        {
            SalesSummaryItems = [NewSummaryItem("客户一", 1, 10, 200m, 0, 0m)],
            SalesSummaryTotal = 1,
        };
        var handler = new GetSalesSummaryRequestHandler(repository);

        var result = await handler.HandleAsync(new GetSalesSummaryRequest { Start = Start, End = End });

        var item = Assert.Single(result.Items);
        Assert.Equal(0, item.ReturnQuantity);
        Assert.Equal(10, item.NetQuantity);
        Assert.Equal(200m, item.NetAmount);
    }
}
