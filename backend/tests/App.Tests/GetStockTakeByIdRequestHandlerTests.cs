using App.Core;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.StockTakes.GetStockTakeById;

using Xunit;

namespace App.Tests;

/// <summary>
/// GetStockTakeByIdRequestHandler 测试（design.md §6）：存在（表头 + 明细映射，含快照字段）/ 不存在 → 40400。
/// </summary>
public class GetStockTakeByIdRequestHandlerTests
{
    [Fact]
    public async Task 盘点详情_存在_应返回表头与明细快照()
    {
        var takes = new FakeStockTakeRepository();
        var handler = new GetStockTakeByIdRequestHandler(takes);

        var take = new StockTake
        {
            Id = Guid.NewGuid(),
            TakeNo = "ST202601010001",
            Type = StockTakeType.Take,
            TakeDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ItemCount = 1,
            DiffItemCount = 1,
            Remark = "盘点备注",
            CreatedAt = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
            UpdatedAt = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero),
        };
        takes.Takes.Add(take);
        takes.Items.Add(new StockTakeItem
        {
            Id = SequentialGuidGenerator.NewSequential(),
            StockTakeId = take.Id,
            ProductId = Guid.NewGuid(),
            ProductCode = "st-code",
            ProductName = "盘点商品",
            Unit = "箱",
            BookQuantity = 5,
            ActualQuantity = 8,
            Difference = 3,
        });

        var result = await handler.HandleAsync(new GetStockTakeByIdRequest { Id = take.Id });

        Assert.Equal(take.Id.ToString(), result.Id);
        Assert.Equal("ST202601010001", result.TakeNo);
        Assert.Equal((int)StockTakeType.Take, result.Type);
        Assert.Equal("盘点备注", result.Remark);

        var item = Assert.Single(result.Items);
        Assert.Equal("st-code", item.ProductCode);
        Assert.Equal("盘点商品", item.ProductName);
        Assert.Equal("箱", item.Unit);
        Assert.Equal(5, item.BookQuantity);
        Assert.Equal(8, item.ActualQuantity);
        Assert.Equal(3, item.Difference);
    }

    [Fact]
    public async Task 盘点详情_不存在_应报NotFound()
    {
        var takes = new FakeStockTakeRepository();
        var handler = new GetStockTakeByIdRequestHandler(takes);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new GetStockTakeByIdRequest { Id = Guid.NewGuid() }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }
}
