using App.Core.Abstractions;
using App.Core.Features.Reports.GetInventoryFlow;

namespace App.Tests;

/// <summary>
/// 进销存报表查询用例测试：筛选 / 分页传参透传、期末列展示、合计为全量口径、空结果。
/// </summary>
public class GetInventoryFlowRequestHandlerTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static InventoryFlowItem NewFlowItem(string code, int opening, int inbound, int outbound)
        => new()
        {
            ProductId = Guid.NewGuid(),
            Code = code,
            Name = $"商品{code}",
            CategoryName = "分类一",
            Unit = "个",
            OpeningQuantity = opening,
            InboundQuantity = inbound,
            OutboundQuantity = outbound,
            ClosingQuantity = opening + inbound - outbound,
        };

    [Fact]
    public async Task 查询_应把期间与筛选分页条件原样传给仓储()
    {
        var repository = new FakeReportQueryRepository();
        var handler = new GetInventoryFlowRequestHandler(repository);
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        await handler.HandleAsync(new GetInventoryFlowRequest
        {
            Start = Start,
            End = End,
            ProductId = productId,
            CategoryId = categoryId,
            OnlyChanged = true,
            Page = 2,
            PageSize = 50,
        });

        var args = repository.LastInventoryFlowArgs;
        Assert.NotNull(args);
        Assert.Equal(Start, args.Value.Start);
        Assert.Equal(End, args.Value.End);
        Assert.Equal(productId, args.Value.ProductId);
        Assert.Equal(categoryId, args.Value.CategoryId);
        Assert.True(args.Value.OnlyChanged);
        Assert.Equal(2, args.Value.Page);
        Assert.Equal(50, args.Value.PageSize);
    }

    [Fact]
    public async Task 查询_应映射行字段并原样带出四列数量()
    {
        var repository = new FakeReportQueryRepository
        {
            InventoryFlowItems = [NewFlowItem("sku-1", opening: 100, inbound: 20, outbound: 30)],
            InventoryFlowTotal = 1,
        };
        var handler = new GetInventoryFlowRequestHandler(repository);

        var result = await handler.HandleAsync(new GetInventoryFlowRequest { Start = Start, End = End });

        var item = Assert.Single(result.Items);
        Assert.Equal("sku-1", item.Code);
        Assert.Equal("分类一", item.CategoryName);
        Assert.Equal("个", item.Unit);
        Assert.Equal(100, item.OpeningQuantity);
        Assert.Equal(20, item.InboundQuantity);
        Assert.Equal(30, item.OutboundQuantity);
        // 期末 = 期初 + 期间入 − 期间出
        Assert.Equal(90, item.ClosingQuantity);
        Assert.Equal(1, result.Total);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public async Task 查询_合计应取仓储的全量口径而非当前页()
    {
        // 当前页只有 1 行，但合计（全量）覆盖 3 行
        var repository = new FakeReportQueryRepository
        {
            InventoryFlowItems = [NewFlowItem("sku-1", 100, 20, 30)],
            InventoryFlowTotal = 3,
            InventoryFlowSummary = new InventoryFlowTotal
            {
                OpeningQuantity = 300,
                InboundQuantity = 60,
                OutboundQuantity = 90,
                ClosingQuantity = 270,
            },
        };
        var handler = new GetInventoryFlowRequestHandler(repository);

        var result = await handler.HandleAsync(new GetInventoryFlowRequest { Start = Start, End = End });

        Assert.Equal(3, result.Total);
        Assert.Equal(300, result.Summary.OpeningQuantity);
        Assert.Equal(60, result.Summary.InboundQuantity);
        Assert.Equal(90, result.Summary.OutboundQuantity);
        Assert.Equal(270, result.Summary.ClosingQuantity);
    }

    [Fact]
    public async Task 查询_无数据时应返回空列表与零合计()
    {
        var repository = new FakeReportQueryRepository { InventoryFlowItems = [], InventoryFlowTotal = 0 };
        var handler = new GetInventoryFlowRequestHandler(repository);

        var result = await handler.HandleAsync(new GetInventoryFlowRequest { Start = Start, End = End });

        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.Summary.ClosingQuantity);
    }
}
