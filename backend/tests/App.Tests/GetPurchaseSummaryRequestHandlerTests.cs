using App.Core.Abstractions;
using App.Core.Features.Reports;
using App.Core.Features.Reports.GetPurchaseSummary;

namespace App.Tests;

/// <summary>
/// 采购汇总查询用例测试：分组维度传参、净额计算、无退货分组、作废过滤透传。
/// </summary>
public class GetPurchaseSummaryRequestHandlerTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static PurchaseSummaryItem NewSummaryItem(string name, int orderCount, int inboundQuantity, decimal inboundAmount, int returnQuantity, decimal returnAmount)
        => new()
        {
            Key = Guid.NewGuid(),
            Name = name,
            Unit = null,
            OrderCount = orderCount,
            InboundQuantity = inboundQuantity,
            InboundAmount = inboundAmount,
            ReturnQuantity = returnQuantity,
            ReturnAmount = returnAmount,
        };

    [Fact]
    public async Task 查询_默认按往来单位维度传参()
    {
        var repository = new FakeReportQueryRepository();
        var handler = new GetPurchaseSummaryRequestHandler(repository);
        var partnerId = Guid.NewGuid();

        await handler.HandleAsync(new GetPurchaseSummaryRequest
        {
            Start = Start,
            End = End,
            PartnerId = partnerId,
            Page = 1,
            PageSize = 20,
        });

        var args = repository.LastPurchaseSummaryArgs;
        Assert.NotNull(args);
        Assert.Equal(Start, args.Value.Start);
        Assert.Equal(End, args.Value.End);
        Assert.Equal(partnerId, args.Value.PartnerId);
        Assert.False(args.Value.GroupByProduct);
    }

    [Fact]
    public async Task 查询_按商品维度时应传递商品分组标记()
    {
        var repository = new FakeReportQueryRepository();
        var handler = new GetPurchaseSummaryRequestHandler(repository);

        await handler.HandleAsync(new GetPurchaseSummaryRequest
        {
            Start = Start,
            End = End,
            GroupBy = SummaryGroupBy.Product,
        });

        var args = repository.LastPurchaseSummaryArgs;
        Assert.NotNull(args);
        Assert.True(args.Value.GroupByProduct);
    }

    [Fact]
    public async Task 查询_净额应为入库减退货()
    {
        var repository = new FakeReportQueryRepository
        {
            PurchaseSummaryItems = [NewSummaryItem("供应商一", 1, 15, 1000m, 3, 300m)],
            PurchaseSummaryTotal = 1,
            PurchaseSummarySummary = new PurchaseSummaryTotal
            {
                OrderCount = 1,
                InboundQuantity = 15,
                InboundAmount = 1000m,
                ReturnQuantity = 3,
                ReturnAmount = 300m,
            },
        };
        var handler = new GetPurchaseSummaryRequestHandler(repository);

        var result = await handler.HandleAsync(new GetPurchaseSummaryRequest { Start = Start, End = End });

        var item = Assert.Single(result.Items);
        Assert.Equal(12, item.NetQuantity);
        Assert.Equal(700m, item.NetAmount);
        Assert.Equal(12, result.Summary.NetQuantity);
        Assert.Equal(700m, result.Summary.NetAmount);
    }

    [Fact]
    public async Task 查询_无退货分组退货列应为零且净额等于入库()
    {
        var repository = new FakeReportQueryRepository
        {
            PurchaseSummaryItems = [NewSummaryItem("供应商二", 2, 8, 500m, 0, 0m)],
            PurchaseSummaryTotal = 1,
        };
        var handler = new GetPurchaseSummaryRequestHandler(repository);

        var result = await handler.HandleAsync(new GetPurchaseSummaryRequest { Start = Start, End = End });

        var item = Assert.Single(result.Items);
        Assert.Equal(0, item.ReturnQuantity);
        Assert.Equal(0m, item.ReturnAmount);
        Assert.Equal(8, item.NetQuantity);
        Assert.Equal(500m, item.NetAmount);
    }

    [Fact]
    public async Task 查询_应原样透传仓储结果_不在用例侧重复过滤()
    {
        // 作废过滤由仓储负责（真库为 Status = Normal 条件）；用例侧不得按行数 / 金额再做裁剪
        var repository = new FakeReportQueryRepository
        {
            PurchaseSummaryItems =
            [
                NewSummaryItem("供应商一", 1, 15, 1000m, 3, 300m),
                NewSummaryItem("供应商二", 0, 0, 0m, 2, 80m),
            ],
            PurchaseSummaryTotal = 2,
        };
        var handler = new GetPurchaseSummaryRequestHandler(repository);

        var result = await handler.HandleAsync(new GetPurchaseSummaryRequest { Start = Start, End = End });

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(-2, result.Items[1].NetQuantity);
        Assert.Equal(-80m, result.Items[1].NetAmount);
    }
}
