using App.Core.Entities;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 结算跨表只读仓储测试（design.md §6）：GetUnsettledAsync（方向 → 单据类型集合映射、过滤已作废 / 已结清）；
/// GetReconciliationAsync（按往来聚合应收 / 应付：含退货冲减与已收 / 已付抵扣、未结单据数）。
/// 纯读查询，用 InMemory 提供程序 + 真实仓储。
/// </summary>
public class SettlementQueryRepositoryTests
{
    private static readonly DateTimeOffset OrderDate = new(2025, 12, 20, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SettlementDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    private static SalesShipment NewSalesShipment(Guid partnerId, string shipmentNo, decimal total, decimal settled, OrderStatus status = OrderStatus.Normal)
        => new()
        {
            Id = Guid.NewGuid(),
            ShipmentNo = shipmentNo,
            PartnerId = partnerId,
            PartnerName = "往来",
            OrderDate = OrderDate,
            TotalAmount = total,
            SettledAmount = settled,
            Status = status,
            CreatedAt = Now,
            UpdatedAt = Now,
        };

    private static PurchaseReceipt NewPurchaseReceipt(Guid partnerId, string receiptNo, decimal total, decimal settled, OrderStatus status = OrderStatus.Normal)
        => new()
        {
            Id = Guid.NewGuid(),
            ReceiptNo = receiptNo,
            PartnerId = partnerId,
            PartnerName = "往来",
            OrderDate = OrderDate,
            TotalAmount = total,
            SettledAmount = settled,
            Status = status,
            CreatedAt = Now,
            UpdatedAt = Now,
        };

    private static PurchaseReturn NewPurchaseReturn(Guid partnerId, string returnNo, decimal total, decimal settled, OrderStatus status = OrderStatus.Normal)
        => new()
        {
            Id = Guid.NewGuid(),
            ReturnNo = returnNo,
            PartnerId = partnerId,
            PartnerName = "往来",
            ReturnDate = OrderDate,
            TotalAmount = total,
            SettledAmount = settled,
            Status = status,
            CreatedAt = Now,
            UpdatedAt = Now,
        };

    private static SalesReturn NewSalesReturn(Guid partnerId, string returnNo, decimal total, decimal settled, OrderStatus status = OrderStatus.Normal)
        => new()
        {
            Id = Guid.NewGuid(),
            ReturnNo = returnNo,
            PartnerId = partnerId,
            PartnerName = "往来",
            ReturnDate = OrderDate,
            TotalAmount = total,
            SettledAmount = settled,
            Status = status,
            CreatedAt = Now,
            UpdatedAt = Now,
        };

    private static Settlement NewSettlement(Guid partnerId, SettlementType type, decimal totalAmount, OrderStatus status = OrderStatus.Normal)
        => new()
        {
            Id = Guid.NewGuid(),
            SettlementNo = type == SettlementType.Receipt ? "RC202601010001" : "PY202601010001",
            Type = type,
            PartnerId = partnerId,
            PartnerName = "往来",
            SettlementDate = SettlementDate,
            TotalAmount = totalAmount,
            Method = SettlementMethod.Cash,
            Status = status,
            CreatedAt = Now,
            UpdatedAt = Now,
        };

    [Fact]
    public async Task 未结候选_收款方向_只返回销售出库单与采购退货单()
    {
        var context = TestSupport.CreateDbContext();
        var customer = TestSupport.NewPartner("客户一", PartnerType.Both);
        context.Partners.Add(customer);

        var salesShipment = NewSalesShipment(customer.Id, "GI202512200001", 1000m, 400m);
        var purchaseReturn = NewPurchaseReturn(customer.Id, "PR202512200001", 200m, 0m);
        var purchaseReceipt = NewPurchaseReceipt(customer.Id, "GR202512200001", 500m, 0m);
        var salesReturn = NewSalesReturn(customer.Id, "SR202512200001", 300m, 0m);
        context.SalesShipments.Add(salesShipment);
        context.PurchaseReturns.Add(purchaseReturn);
        context.PurchaseReceipts.Add(purchaseReceipt);
        context.SalesReturns.Add(salesReturn);
        await context.SaveChangesAsync();

        var repository = new SettlementQueryRepository(context);

        var (items, total) = await repository.GetUnsettledAsync(customer.Id, SettlementType.Receipt, 1, 20);

        Assert.Equal(2, total);
        Assert.Equal(
            new[] { SettlementOrderType.SalesOutbound, SettlementOrderType.PurchaseReturn },
            items.Select(i => i.OrderType).OrderBy(t => t).ToArray());
        var salesCandidate = items.Single(i => i.OrderType == SettlementOrderType.SalesOutbound);
        Assert.Equal(1000m, salesCandidate.TotalAmount);
        Assert.Equal(400m, salesCandidate.SettledAmount);
        Assert.Equal(600m, salesCandidate.UnsettledAmount);
    }

    [Fact]
    public async Task 未结候选_付款方向_只返回采购入库单与销售退货单()
    {
        var context = TestSupport.CreateDbContext();
        var supplier = TestSupport.NewPartner("供应商一", PartnerType.Both);
        context.Partners.Add(supplier);

        context.SalesShipments.Add(NewSalesShipment(supplier.Id, "GI202512200001", 1000m, 0m));
        context.PurchaseReturns.Add(NewPurchaseReturn(supplier.Id, "PR202512200001", 200m, 0m));
        context.PurchaseReceipts.Add(NewPurchaseReceipt(supplier.Id, "GR202512200001", 500m, 0m));
        context.SalesReturns.Add(NewSalesReturn(supplier.Id, "SR202512200001", 300m, 0m));
        await context.SaveChangesAsync();

        var repository = new SettlementQueryRepository(context);

        var (items, total) = await repository.GetUnsettledAsync(supplier.Id, SettlementType.Payment, 1, 20);

        Assert.Equal(2, total);
        Assert.Equal(
            new[] { SettlementOrderType.PurchaseInbound, SettlementOrderType.SalesReturn },
            items.Select(i => i.OrderType).OrderBy(t => t).ToArray());
    }

    [Fact]
    public async Task 未结候选_应过滤已作废与已结清单据_并支持分页()
    {
        var context = TestSupport.CreateDbContext();
        var customer = TestSupport.NewPartner("客户一", PartnerType.Customer);
        context.Partners.Add(customer);

        context.SalesShipments.Add(NewSalesShipment(customer.Id, "GI202512200001", 1000m, 0m));
        context.SalesShipments.Add(NewSalesShipment(customer.Id, "GI202512200002", 1000m, 1000m)); // 已结清
        context.SalesShipments.Add(NewSalesShipment(customer.Id, "GI202512200003", 1000m, 0m, OrderStatus.Voided)); // 已作废
        await context.SaveChangesAsync();

        var repository = new SettlementQueryRepository(context);

        var (firstPage, total) = await repository.GetUnsettledAsync(customer.Id, SettlementType.Receipt, 1, 1);

        Assert.Equal(1, total);
        Assert.Single(firstPage);
        Assert.Equal("GI202512200001", firstPage[0].OrderNo);
    }

    [Fact]
    public async Task 往来台账_应收应付_应含退货冲减与已收已付抵扣()
    {
        var context = TestSupport.CreateDbContext();
        var customer = TestSupport.NewPartner("客户一", PartnerType.Customer);
        context.Partners.Add(customer);

        // 应收 = 销售 1000 − 销售退货 300 − 已收 400 = 300（退货单已结清，不影响未结单据数）
        context.SalesShipments.Add(NewSalesShipment(customer.Id, "GI202512200001", 1000m, 400m));
        context.SalesReturns.Add(NewSalesReturn(customer.Id, "SR202512200001", 300m, 300m));
        context.Settlements.Add(NewSettlement(customer.Id, SettlementType.Receipt, 400m));
        // 已作废收款单不计入已收
        context.Settlements.Add(NewSettlement(customer.Id, SettlementType.Receipt, 999m, OrderStatus.Voided));
        // 应付 = 采购 500 − 采购退货 100 − 已付 200 = 200
        context.PurchaseReceipts.Add(NewPurchaseReceipt(customer.Id, "GR202512200001", 500m, 200m));
        context.PurchaseReturns.Add(NewPurchaseReturn(customer.Id, "PR202512200001", 100m, 100m));
        context.Settlements.Add(NewSettlement(customer.Id, SettlementType.Payment, 200m));
        await context.SaveChangesAsync();

        var repository = new SettlementQueryRepository(context);

        var (items, total) = await repository.GetReconciliationAsync(null, null, 1, 20);

        Assert.Equal(1, total);
        var row = Assert.Single(items);
        Assert.Equal(customer.Id, row.PartnerId);
        Assert.Equal("客户一", row.PartnerName);
        Assert.Equal(300m, row.ReceivableAmount);
        Assert.Equal(200m, row.PayableAmount);
        // 未结单据数：销售单（未结）、采购单（未结）= 2；退货单均已结清 / 无未结
        Assert.Equal(2, row.UnsettledOrderCount);
    }

    [Fact]
    public async Task 往来台账_应按关键词与类型筛选()
    {
        var context = TestSupport.CreateDbContext();
        var customer = TestSupport.NewPartner("北京客户", PartnerType.Customer);
        var supplier = TestSupport.NewPartner("上海供应商", PartnerType.Supplier);
        context.Partners.AddRange(customer, supplier);
        await context.SaveChangesAsync();

        var repository = new SettlementQueryRepository(context);

        var (byKeyword, keywordTotal) = await repository.GetReconciliationAsync("北京", null, 1, 20);
        Assert.Equal(1, keywordTotal);
        Assert.Equal(customer.Id, Assert.Single(byKeyword).PartnerId);

        var (byType, typeTotal) = await repository.GetReconciliationAsync(null, PartnerType.Supplier, 1, 20);
        Assert.Equal(1, typeTotal);
        Assert.Equal(supplier.Id, Assert.Single(byType).PartnerId);
    }
}
