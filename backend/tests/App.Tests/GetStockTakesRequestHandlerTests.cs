using App.Core.Entities;
using App.Core.Features.StockTakes.GetStockTakes;

using Xunit;

namespace App.Tests;

/// <summary>
/// GetStockTakesRequestHandler 测试（design.md §6）：筛选传参组合（keyword / type / 日期范围）+ 分页映射
/// （ItemCount / DiffItemCount / Type 透传），CreatedAt DESC 排序。
/// </summary>
public class GetStockTakesRequestHandlerTests
{
    private static (FakeStockTakeRepository Takes, GetStockTakesRequestHandler Handler) CreateHandler()
    {
        var takes = new FakeStockTakeRepository();
        var handler = new GetStockTakesRequestHandler(takes);
        return (takes, handler);
    }

    private static void SeedTake(FakeStockTakeRepository takes, string takeNo, StockTakeType type,
        DateTimeOffset takeDate, DateTimeOffset createdAt, int itemCount = 3, int diffCount = 1, string? remark = null)
    {
        takes.Takes.Add(new StockTake
        {
            Id = Guid.NewGuid(),
            TakeNo = takeNo,
            Type = type,
            TakeDate = takeDate,
            ItemCount = itemCount,
            DiffItemCount = diffCount,
            Remark = remark,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        });
    }

    [Fact]
    public async Task 盘点列表_按单号关键词筛选_命中返回()
    {
        var (takes, handler) = CreateHandler();
        SeedTake(takes, "ST202601010001", StockTakeType.Take, new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero));
        SeedTake(takes, "ST202601010002", StockTakeType.Initial, new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new(2026, 1, 2, 8, 0, 0, TimeSpan.Zero));

        var result = await handler.HandleAsync(new GetStockTakesRequest { Keyword = "st202601010001" });

        var item = Assert.Single(result.Items);
        Assert.Equal("ST202601010001", item.TakeNo);
        Assert.Equal(1, result.Total);
    }

    [Fact]
    public async Task 盘点列表_按类型与日期范围筛选_仅命中区间内()
    {
        var (takes, handler) = CreateHandler();
        SeedTake(takes, "ST202601010001", StockTakeType.Take, new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero));
        SeedTake(takes, "ST202601020001", StockTakeType.Take, new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero), new(2026, 1, 2, 8, 0, 0, TimeSpan.Zero));
        SeedTake(takes, "ST202601030001", StockTakeType.Initial, new(2026, 1, 3, 0, 0, 0, TimeSpan.Zero), new(2026, 1, 3, 8, 0, 0, TimeSpan.Zero));

        var result = await handler.HandleAsync(new GetStockTakesRequest
        {
            Type = StockTakeType.Take,
            Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            End = new(2026, 1, 2, 23, 59, 59, TimeSpan.Zero),
        });

        Assert.Equal(2, result.Total);
        Assert.All(result.Items, i => Assert.Equal((int)StockTakeType.Take, i.Type));
    }

    [Fact]
    public async Task 盘点列表_分页映射_透传ItemCount_DiffItemCount_Type且按创建时间倒序()
    {
        var (takes, handler) = CreateHandler();
        SeedTake(takes, "ST202601010001", StockTakeType.Take, new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero), itemCount: 5, diffCount: 2, remark: "差异说明");
        SeedTake(takes, "ST202601010002", StockTakeType.Initial, new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new(2026, 1, 2, 8, 0, 0, TimeSpan.Zero), itemCount: 3, diffCount: 0);

        var result = await handler.HandleAsync(new GetStockTakesRequest { Page = 1, PageSize = 2 });

        Assert.Equal(2, result.Total);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);

        // 创建时间倒序：第二张（1/2）在前
        var first = result.Items[0];
        Assert.Equal("ST202601010002", first.TakeNo);
        Assert.Equal(3, first.ItemCount);
        Assert.Equal(0, first.DiffItemCount);
        Assert.Equal((int)StockTakeType.Initial, first.Type);

        var second = result.Items[1];
        Assert.Equal("ST202601010001", second.TakeNo);
        Assert.Equal(5, second.ItemCount);
        Assert.Equal(2, second.DiffItemCount);
        Assert.Equal((int)StockTakeType.Take, second.Type);
        Assert.Equal("差异说明", second.Remark);
    }
}
