using App.Core.Entities;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 收付款单仓储分页查询测试（design.md §6）：按被核销单据反查（<c>SettlementItems.OrderId</c> + <c>OrderType</c>）。
/// 纯读查询，用 InMemory 提供程序 + 真实仓储。
/// </summary>
public class SettlementRepositoryTests
{
    private static readonly DateTimeOffset SettlementDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Settlement NewSettlement(string settlementNo, OrderStatus status = OrderStatus.Normal)
        => new()
        {
            Id = Guid.NewGuid(),
            SettlementNo = settlementNo,
            Type = SettlementType.Receipt,
            PartnerId = Guid.NewGuid(),
            PartnerName = "往来",
            SettlementDate = SettlementDate,
            TotalAmount = 100m,
            Method = SettlementMethod.Cash,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static SettlementItem NewItem(Guid settlementId, SettlementOrderType orderType, Guid orderId, decimal amount)
        => new()
        {
            Id = Guid.NewGuid(),
            SettlementId = settlementId,
            OrderType = orderType,
            OrderId = orderId,
            OrderNo = "GI202512200001",
            OrderDate = SettlementDate,
            OrderTotalAmount = 1000m,
            Amount = amount,
        };

    [Fact]
    public async Task 按单据反查_只返回核销了该单据的收付款单_含已作废()
    {
        var context = TestSupport.CreateDbContext();
        var targetOrderId = Guid.NewGuid();
        var matched = NewSettlement("RC202601010001");
        var matchedVoided = NewSettlement("RC202601010002", OrderStatus.Voided);
        var other = NewSettlement("RC202601010003");
        context.Settlements.AddRange(matched, matchedVoided, other);
        context.SettlementItems.AddRange(
            NewItem(matched.Id, SettlementOrderType.SalesOutbound, targetOrderId, 60m),
            NewItem(matchedVoided.Id, SettlementOrderType.SalesOutbound, targetOrderId, 40m),
            NewItem(other.Id, SettlementOrderType.SalesOutbound, Guid.NewGuid(), 100m));
        await context.SaveChangesAsync();

        var (items, total) = await new SettlementRepository(context).GetPagedAsync(
            null, null, null, null, null, null,
            SettlementOrderType.SalesOutbound, targetOrderId, 1, 20);

        Assert.Equal(2, total);
        Assert.Equal(
            new[] { "RC202601010001", "RC202601010002" },
            items.Select(s => s.SettlementNo).OrderBy(n => n, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task 按单据反查_单据类型不匹配时不应命中()
    {
        var context = TestSupport.CreateDbContext();
        var targetOrderId = Guid.NewGuid();
        var settlement = NewSettlement("RC202601010001");
        context.Settlements.Add(settlement);
        context.SettlementItems.Add(NewItem(settlement.Id, SettlementOrderType.SalesOutbound, targetOrderId, 100m));
        await context.SaveChangesAsync();

        var (items, total) = await new SettlementRepository(context).GetPagedAsync(
            null, null, null, null, null, null,
            SettlementOrderType.PurchaseInbound, targetOrderId, 1, 20);

        Assert.Equal(0, total);
        Assert.Empty(items);
    }

    [Fact]
    public async Task 未传单据条件_不做反查过滤()
    {
        var context = TestSupport.CreateDbContext();
        var settlement = NewSettlement("RC202601010001");
        context.Settlements.Add(settlement);
        context.SettlementItems.Add(NewItem(settlement.Id, SettlementOrderType.SalesOutbound, Guid.NewGuid(), 100m));
        await context.SaveChangesAsync();

        var (items, total) = await new SettlementRepository(context).GetPagedAsync(
            null, null, null, null, null, null, null, null, 1, 20);

        Assert.Equal(1, total);
        Assert.Single(items);
    }
}
