using App.Core.Entities;
using App.Core.Features.SalesShipments.GetSalesShipments;

namespace App.Tests;

/// <summary>
/// GetSalesShipmentsRequestHandler 测试：筛选入参透传、分页映射与**数量合计**透传
/// （销售订单详情「关联出库单」按数量合计跟单），与采购侧同构。
/// </summary>
public class GetSalesShipmentsRequestHandlerTests
{
    private static SalesShipment NewShipment(string shipmentNo, decimal totalAmount = 200m, decimal settledAmount = 0m)
    {
        var now = DateTimeOffset.UtcNow;
        return new SalesShipment
        {
            Id = Guid.NewGuid(),
            ShipmentNo = shipmentNo,
            PartnerId = Guid.NewGuid(),
            PartnerName = "客户一",
            OrderDate = now,
            TotalAmount = totalAmount,
            SettledAmount = settledAmount,
            Status = OrderStatus.Normal,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    [Fact]
    public async Task 查询出库单列表_应透传筛选入参并按分页映射()
    {
        var repository = new FakeSalesShipmentRepository();
        var orderId = Guid.NewGuid();
        var shipment = NewShipment("GI202601010001", settledAmount: 50m); // 部分结算（200 中已收 50）
        repository.PagedItems = new[] { (Order: shipment, TotalQuantity: 4) };
        repository.PagedTotal = 3;
        var handler = new GetSalesShipmentsRequestHandler(repository);

        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.Zero);
        var result = await handler.HandleAsync(new GetSalesShipmentsRequest
        {
            Page = 2,
            PageSize = 10,
            Keyword = "GI2026",
            OrderId = orderId,
            Start = start,
            End = end,
            SettlementState = SettlementState.PartiallySettled,
        });

        var query = Assert.Single(repository.PagedQueries);
        Assert.Equal("GI2026", query.Keyword);
        Assert.Equal(orderId, query.OrderId);
        Assert.Equal(start, query.Start);
        Assert.Equal(end, query.End);
        Assert.Equal(SettlementState.PartiallySettled, query.SettlementState);
        Assert.Equal(2, query.Page);
        Assert.Equal(10, query.PageSize);

        Assert.Equal(3, result.Total);
        var row = Assert.Single(result.Items);
        Assert.Equal("GI202601010001", row.ShipmentNo);
        Assert.Equal(4, row.TotalQuantity);
        Assert.Equal(150m, row.UnsettledAmount);
        Assert.Equal((int)SettlementState.PartiallySettled, row.SettlementState);
    }

    [Fact]
    public async Task 查询出库单列表_数量合计按单据随行返回()
    {
        var repository = new FakeSalesShipmentRepository
        {
            PagedItems = new[]
            {
                (Order: NewShipment("GI202601010001"), TotalQuantity: 0),
                (Order: NewShipment("GI202601010002"), TotalQuantity: 9),
            },
            PagedTotal = 2,
        };

        var result = await new GetSalesShipmentsRequestHandler(repository)
            .HandleAsync(new GetSalesShipmentsRequest());

        Assert.Equal(2, result.Total);
        Assert.Equal(0, result.Items.Single(r => r.ShipmentNo == "GI202601010001").TotalQuantity);
        Assert.Equal(9, result.Items.Single(r => r.ShipmentNo == "GI202601010002").TotalQuantity);
    }
}
