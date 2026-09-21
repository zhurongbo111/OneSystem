using App.Core.Entities;
using App.Core.Features.PurchaseReceipts.GetPurchaseReceipts;

namespace App.Tests;

/// <summary>
/// GetPurchaseReceiptsRequestHandler 测试：筛选入参透传、分页映射与**数量合计**透传
/// （订单详情「关联入库单」按数量合计跟单）。
/// </summary>
public class GetPurchaseReceiptsRequestHandlerTests
{
    private static PurchaseReceipt NewReceipt(string receiptNo, decimal totalAmount = 100m)
    {
        var now = DateTimeOffset.UtcNow;
        return new PurchaseReceipt
        {
            Id = Guid.NewGuid(),
            ReceiptNo = receiptNo,
            PartnerId = Guid.NewGuid(),
            PartnerName = "供应商一",
            OrderDate = now,
            TotalAmount = totalAmount,
            SettledAmount = 0m,
            Status = OrderStatus.Normal,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    [Fact]
    public async Task 查询入库单列表_应透传筛选入参并按分页映射()
    {
        var repository = new FakePurchaseReceiptRepository();
        var orderId = Guid.NewGuid();
        var receipt = NewReceipt("GR202601010001");
        repository.PagedItems = new[] { (Order: receipt, TotalQuantity: 7) };
        repository.PagedTotal = 3;
        var handler = new GetPurchaseReceiptsRequestHandler(repository);

        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.Zero);
        var result = await handler.HandleAsync(new GetPurchaseReceiptsRequest
        {
            Page = 2,
            PageSize = 10,
            Keyword = "GR2026",
            OrderId = orderId,
            Start = start,
            End = end,
            SettlementState = SettlementState.Unsettled,
        });

        // 筛选入参原样透传
        var query = Assert.Single(repository.PagedQueries);
        Assert.Equal("GR2026", query.Keyword);
        Assert.Equal(orderId, query.OrderId);
        Assert.Equal(start, query.Start);
        Assert.Equal(end, query.End);
        Assert.Equal(SettlementState.Unsettled, query.SettlementState);
        Assert.Equal(2, query.Page);
        Assert.Equal(10, query.PageSize);

        // 分页映射 + 数量合计（仓储聚合值原样透传）
        Assert.Equal(3, result.Total);
        var row = Assert.Single(result.Items);
        Assert.Equal("GR202601010001", row.ReceiptNo);
        Assert.Equal(7, row.TotalQuantity);
        Assert.Equal((int)SettlementState.Unsettled, row.SettlementState);
    }

    [Fact]
    public async Task 查询入库单列表_数量合计按单据随行返回()
    {
        var repository = new FakePurchaseReceiptRepository
        {
            PagedItems = new[]
            {
                (Order: NewReceipt("GR202601010001"), TotalQuantity: 0),
                (Order: NewReceipt("GR202601010002"), TotalQuantity: 12),
            },
            PagedTotal = 2,
        };

        var result = await new GetPurchaseReceiptsRequestHandler(repository)
            .HandleAsync(new GetPurchaseReceiptsRequest());

        Assert.Equal(2, result.Total);
        Assert.Equal(0, result.Items.Single(r => r.ReceiptNo == "GR202601010001").TotalQuantity);
        Assert.Equal(12, result.Items.Single(r => r.ReceiptNo == "GR202601010002").TotalQuantity);
    }
}
