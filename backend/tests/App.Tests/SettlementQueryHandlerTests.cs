using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Settlements.GetReconciliation;
using App.Core.Features.Settlements.GetSettlementById;
using App.Core.Features.Settlements.GetSettlements;
using App.Core.Features.Settlements.GetUnsettledOrders;

namespace App.Tests;

/// <summary>
/// 收付款查询用例测试（design.md §6）：GetSettlements（筛选透传 + 分页映射 + 含作废）；
/// GetSettlementById（核销明细快照透传；不存在 40400）；
/// GetUnsettledOrders（往来 / 方向透传 + 未结金额映射）；GetReconciliation（往来 / 类型透传 + 聚合映射）。
/// </summary>
public class SettlementQueryHandlerTests
{
    private static readonly DateTimeOffset SettlementDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset OrderDate = new(2025, 12, 20, 0, 0, 0, TimeSpan.Zero);

    private static Settlement NewSettlement(
        SettlementType type = SettlementType.Receipt,
        OrderStatus status = OrderStatus.Normal,
        decimal totalAmount = 400m)
    {
        var now = DateTimeOffset.UtcNow;
        return new Settlement
        {
            Id = Guid.NewGuid(),
            SettlementNo = type == SettlementType.Receipt ? "RC202601010001" : "PY202601010001",
            Type = type,
            PartnerId = Guid.NewGuid(),
            PartnerName = "往来一",
            SettlementDate = SettlementDate,
            TotalAmount = totalAmount,
            Method = SettlementMethod.BankTransfer,
            Status = status,
            Remark = "备注一",
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static SettlementItem NewItem(Guid settlementId, SettlementOrderType orderType, decimal amount)
        => new()
        {
            Id = Guid.NewGuid(),
            SettlementId = settlementId,
            OrderType = orderType,
            OrderId = Guid.NewGuid(),
            OrderNo = "GI202512200001",
            OrderDate = OrderDate,
            OrderTotalAmount = 1000m,
            Amount = amount,
        };

    [Fact]
    public async Task 查询收付款单列表_应透传筛选入参并按分页映射()
    {
        var repository = new FakeSettlementRepository();
        var settlement = NewSettlement();
        repository.PagedItems = new[] { settlement };
        repository.PagedTotal = 5;
        settlement.Remark = "备注一";
        // 混合核销两类单据：列表「单据类型」列按明细去重升序派生
        repository.Seed(settlement, new[]
        {
            NewItem(settlement.Id, SettlementOrderType.PurchaseReturn, 100m),
            NewItem(settlement.Id, SettlementOrderType.SalesOutbound, 300m),
        });
        var handler = new GetSettlementsRequestHandler(repository);

        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.Zero);
        var result = await handler.HandleAsync(new GetSettlementsRequest
        {
            Page = 2,
            PageSize = 10,
            Keyword = "RC2026",
            Type = SettlementType.Receipt,
            PartnerId = settlement.PartnerId,
            Method = SettlementMethod.BankTransfer,
            Start = start,
            End = end,
        });

        // 筛选入参原样透传
        var query = Assert.Single(repository.PagedQueries);
        Assert.Equal("RC2026", query.Keyword);
        Assert.Equal(SettlementType.Receipt, query.Type);
        Assert.Equal(settlement.PartnerId, query.PartnerId);
        Assert.Equal(SettlementMethod.BankTransfer, query.Method);
        Assert.Equal(start, query.Start);
        Assert.Equal(end, query.End);
        Assert.Equal(2, query.Page);
        Assert.Equal(10, query.PageSize);

        // 分页结果映射（含 Status 供前端作废行置灰）
        Assert.Equal(5, result.Total);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        var row = Assert.Single(result.Items);
        Assert.Equal(settlement.SettlementNo, row.SettlementNo);
        Assert.Equal((int)SettlementType.Receipt, row.Type);
        Assert.Equal(settlement.PartnerName, row.PartnerName);
        Assert.Equal(400m, row.TotalAmount);
        Assert.Equal((int)SettlementMethod.BankTransfer, row.Method);
        Assert.Equal((int)OrderStatus.Normal, row.Status);
        // 「单据类型」列：按核销明细去重升序派生（销售出库 1 + 采购退货 2）
        Assert.Equal(
            new[] { (int)SettlementOrderType.SalesOutbound, (int)SettlementOrderType.PurchaseReturn },
            row.OrderTypes);
    }

    [Fact]
    public async Task 查询收付款单列表_含作废单据_应返回作废行()
    {
        var repository = new FakeSettlementRepository();
        var voided = NewSettlement(status: OrderStatus.Voided);
        repository.PagedItems = new[] { voided };
        repository.PagedTotal = 1;

        var result = await new GetSettlementsRequestHandler(repository).HandleAsync(new GetSettlementsRequest());

        var row = Assert.Single(result.Items);
        Assert.Equal((int)OrderStatus.Voided, row.Status);
    }

    [Fact]
    public async Task 按被核销单据反查_应透传条件并附本次核销金额()
    {
        var calls = new List<string>();
        var repository = new FakeSettlementRepository(calls);
        var orderId = Guid.NewGuid();
        var matched = NewSettlement(totalAmount: 300m);
        var other = NewSettlement(totalAmount: 100m);
        repository.PagedItems = new[] { matched, other };
        repository.PagedTotal = 2;
        repository.Seed(matched, new[]
        {
            new SettlementItem
            {
                Id = Guid.NewGuid(),
                SettlementId = matched.Id,
                OrderType = SettlementOrderType.SalesOutbound,
                OrderId = orderId,
                OrderNo = "GI202512200001",
                OrderDate = OrderDate,
                OrderTotalAmount = 1000m,
                Amount = 300m,
            },
        });
        repository.Seed(other, new[]
        {
            new SettlementItem
            {
                Id = Guid.NewGuid(),
                SettlementId = other.Id,
                OrderType = SettlementOrderType.SalesOutbound,
                OrderId = Guid.NewGuid(), // 核销的是别的单据
                OrderNo = "GI202512200002",
                OrderDate = OrderDate,
                OrderTotalAmount = 100m,
                Amount = 100m,
            },
        });

        var result = await new GetSettlementsRequestHandler(repository).HandleAsync(new GetSettlementsRequest
        {
            OrderType = SettlementOrderType.SalesOutbound,
            OrderId = orderId,
        });

        // 反查条件原样透传 + 批量取核销明细
        var query = Assert.Single(repository.PagedQueries);
        Assert.Equal(SettlementOrderType.SalesOutbound, query.OrderType);
        Assert.Equal(orderId, query.OrderId);
        Assert.Contains("GetItemsBySettlementIds", calls);

        // 命中该单据的收付款单附本次核销金额；未命中该单据的为 null
        Assert.Equal(300m, result.Items.Single(r => r.Id == matched.Id.ToString()).OrderAmount);
        Assert.Null(result.Items.Single(r => r.Id == other.Id.ToString()).OrderAmount);
    }

    [Fact]
    public async Task 查询收付款单列表_未按单据反查_OrderAmount为空且单据类型为空集合()
    {
        var calls = new List<string>();
        var repository = new FakeSettlementRepository(calls);
        repository.PagedItems = new[] { NewSettlement() };
        repository.PagedTotal = 1;

        var result = await new GetSettlementsRequestHandler(repository).HandleAsync(new GetSettlementsRequest());

        var row = Assert.Single(result.Items);
        Assert.Null(row.OrderAmount);
        Assert.Empty(row.OrderTypes);
        // 未指定被核销单据：仍需批量取本页明细以聚合「单据类型」列（只查一次，不是逐单 N+1）
        Assert.Contains("GetItemsBySettlementIds", calls);
    }

    [Fact]
    public async Task 查询收付款单详情_存在_应返回核销明细快照()
    {
        var repository = new FakeSettlementRepository();
        var settlement = NewSettlement();
        var item = new SettlementItem
        {
            Id = Guid.NewGuid(),
            SettlementId = settlement.Id,
            OrderType = SettlementOrderType.SalesOutbound,
            OrderId = Guid.NewGuid(),
            OrderNo = "GI202512200001",
            OrderDate = OrderDate,
            OrderTotalAmount = 1000m,
            Amount = 400m,
        };
        repository.Seed(settlement, new[] { item });

        var result = await new GetSettlementByIdRequestHandler(repository).HandleAsync(
            new GetSettlementByIdRequest { Id = settlement.Id });

        Assert.Equal(settlement.SettlementNo, result.SettlementNo);
        Assert.Equal(400m, result.TotalAmount);
        var line = Assert.Single(result.Items);
        Assert.Equal((int)SettlementOrderType.SalesOutbound, line.OrderType);
        Assert.Equal("GI202512200001", line.OrderNo);
        Assert.Equal(OrderDate, line.OrderDate);
        Assert.Equal(1000m, line.OrderTotalAmount);
        Assert.Equal(400m, line.Amount);
    }

    [Fact]
    public async Task 查询收付款单详情_不存在_应报NotFound()
    {
        var repository = new FakeSettlementRepository();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => new GetSettlementByIdRequestHandler(repository).HandleAsync(
            new GetSettlementByIdRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 查询可核销单据_应透传往来与方向并映射未结金额()
    {
        var repository = new FakeSettlementQueryRepository();
        var partnerId = Guid.NewGuid();
        repository.UnsettledItems = new[]
        {
            new SettlementCandidateItem
            {
                OrderType = SettlementOrderType.SalesOutbound,
                OrderId = Guid.NewGuid(),
                OrderNo = "GI202512200001",
                OrderDate = OrderDate,
                TotalAmount = 1000m,
                SettledAmount = 400m,
            },
        };
        repository.UnsettledTotal = 1;

        var result = await new GetUnsettledOrdersRequestHandler(repository).HandleAsync(new GetUnsettledOrdersRequest
        {
            PartnerId = partnerId,
            Type = SettlementType.Receipt,
            Page = 2,
            PageSize = 10,
        });

        var query = Assert.Single(repository.UnsettledQueries);
        Assert.Equal(partnerId, query.PartnerId);
        Assert.Equal(SettlementType.Receipt, query.Type);
        Assert.Equal(2, query.Page);
        Assert.Equal(10, query.PageSize);

        Assert.Equal(1, result.Total);
        var row = Assert.Single(result.Items);
        Assert.Equal((int)SettlementOrderType.SalesOutbound, row.OrderType);
        Assert.Equal(1000m, row.TotalAmount);
        Assert.Equal(400m, row.SettledAmount);
        Assert.Equal(600m, row.UnsettledAmount); // 未结金额由读模型推导
    }

    [Fact]
    public async Task 查询往来对账_应透传往来类型并映射应收应付()
    {
        var repository = new FakeSettlementQueryRepository();
        repository.ReconciliationItems = new[]
        {
            new ReconciliationItem
            {
                PartnerId = Guid.NewGuid(),
                PartnerName = "客户一",
                PartnerType = PartnerType.Customer,
                ReceivableAmount = 600m,
                PayableAmount = 0m,
                UnsettledOrderCount = 2,
            },
        };
        repository.ReconciliationTotal = 1;

        var result = await new GetReconciliationRequestHandler(repository).HandleAsync(new GetReconciliationRequest
        {
            Page = 1,
            PageSize = 20,
            Keyword = "客户",
            Type = PartnerType.Customer,
        });

        var query = Assert.Single(repository.ReconciliationQueries);
        Assert.Equal("客户", query.Keyword);
        Assert.Equal(PartnerType.Customer, query.Type);
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);

        var row = Assert.Single(result.Items);
        Assert.Equal("客户一", row.PartnerName);
        Assert.Equal((int)PartnerType.Customer, row.PartnerType);
        Assert.Equal(600m, row.ReceivableAmount);
        Assert.Equal(0m, row.PayableAmount);
        Assert.Equal(2, row.UnsettledOrderCount);
    }
}
