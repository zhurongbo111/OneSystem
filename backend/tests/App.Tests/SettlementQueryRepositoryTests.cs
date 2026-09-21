using App.Core.Entities;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 结算跨表只读仓储测试（design.md §6）：GetUnsettledAsync（方向 → 单据类型集合映射、过滤已作废 / 已结清）；
/// GetReconciliationAsync（按往来聚合应收 / 应付：按被核销单据未结金额归集、未结单据数；不引用收付款单类型）。
/// 纯读查询，用 InMemory 提供程序 + 真实仓储。
/// </summary>
public class SettlementQueryRepositoryTests
{
    private static readonly DateTimeOffset OrderDate = new(2025, 12, 20, 0, 0, 0, TimeSpan.Zero);

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
    public async Task 往来台账_应收应付_应按被核销单据未结金额归集()
    {
        var context = TestSupport.CreateDbContext();
        var customer = TestSupport.NewPartner("客户一", PartnerType.Customer);
        context.Partners.Add(customer);

        // 应收 = 销售出库未结 600（1000 − 400）+ 采购退货未结 100（100 − 0）= 700
        context.SalesShipments.Add(NewSalesShipment(customer.Id, "GI202512200001", 1000m, 400m));
        context.PurchaseReturns.Add(NewPurchaseReturn(customer.Id, "PR202512200001", 100m, 0m));
        // 应付 = 采购入库未结 300（500 − 200）+ 销售退货未结 0（300 − 300）= 300
        context.PurchaseReceipts.Add(NewPurchaseReceipt(customer.Id, "GR202512200001", 500m, 200m));
        context.SalesReturns.Add(NewSalesReturn(customer.Id, "SR202512200001", 300m, 300m));
        // 已作废单据不计入未结
        context.SalesShipments.Add(NewSalesShipment(customer.Id, "GI202512200002", 999m, 0m, OrderStatus.Voided));
        await context.SaveChangesAsync();

        var repository = new SettlementQueryRepository(context);

        var (items, total) = await repository.GetReconciliationAsync(null, null, 1, 20);

        Assert.Equal(1, total);
        var row = Assert.Single(items);
        Assert.Equal(customer.Id, row.PartnerId);
        Assert.Equal("客户一", row.PartnerName);
        Assert.Equal(700m, row.ReceivableAmount);
        Assert.Equal(300m, row.PayableAmount);
        // 未结单据数：销售出库（未结）、采购退货（未结）、采购入库（未结）= 3；销售退货已结清、已作废不计
        Assert.Equal(3, row.UnsettledOrderCount);
    }

    [Fact]
    public async Task 往来台账_收款挂供应商_余额应按未结金额归集()
    {
        var context = TestSupport.CreateDbContext();
        var settledSupplier = TestSupport.NewPartner("已退款供应商", PartnerType.Supplier);
        var pendingSupplier = TestSupport.NewPartner("未退款供应商", PartnerType.Supplier);
        context.Partners.AddRange(settledSupplier, pendingSupplier);

        // 已退款供应商：采购入库 800 未付 → 应付 800；采购退货 500 已由收款单收回退款 → 未结 0（旧口径会把 500 算成负应收）
        context.PurchaseReceipts.Add(NewPurchaseReceipt(settledSupplier.Id, "GR202512200001", 800m, 0m));
        context.PurchaseReturns.Add(NewPurchaseReturn(settledSupplier.Id, "PR202512200001", 500m, 500m));
        // 未退款供应商：采购退货 200 未收回 → 挂应收 200
        context.PurchaseReturns.Add(NewPurchaseReturn(pendingSupplier.Id, "PR202512200002", 200m, 0m));
        await context.SaveChangesAsync();

        var repository = new SettlementQueryRepository(context);

        var (items, total) = await repository.GetReconciliationAsync(null, null, 1, 20);

        Assert.Equal(2, total);
        var settled = items.Single(i => i.PartnerId == settledSupplier.Id);
        Assert.Equal(0m, settled.ReceivableAmount);
        Assert.Equal(800m, settled.PayableAmount);
        Assert.Equal(1, settled.UnsettledOrderCount);

        var pending = items.Single(i => i.PartnerId == pendingSupplier.Id);
        Assert.Equal(200m, pending.ReceivableAmount);
        Assert.Equal(0m, pending.PayableAmount);
        Assert.Equal(1, pending.UnsettledOrderCount);
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
