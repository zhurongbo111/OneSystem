using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Features.Partners.UpdatePartner;
using App.Core.Features.SalesShipments.GetSalesShipmentById;
using App.Core.Features.Settlements.GetReconciliation;
using App.Infrastructure;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// 往来单位账期与信用额度测试（specs/036-erp-partner-price/design.md §0.2 / §0.3）：
/// 到期日 = 单据日期 + 账期天数（推导不落列）；逾期 = 未结清且到期日早于今天；
/// 已结清 / 已作废单据不参与逾期；「仅看逾期」为派生值过滤。
/// 编辑往来单位时两字段按全量覆盖语义处理（缺字段清 0）。
/// 逾期派生与 filter 在真实仓储上验证（纯读查询，InMemory 可用）；Handler 侧用行为型假实现验证入参透传与出参映射。
/// </summary>
public class PartnerPaymentTermTests
{
    private static readonly DateTimeOffset BaseDate = new(2025, 12, 20, 0, 0, 0, TimeSpan.Zero);

    // 逾期判定基准日固定为 2025-12-28（由用例注入，仓储不读系统时间）
    private static readonly DateOnly Today = new(2025, 12, 28);

    private static async Task<Partner> SeedCustomerAsync(AppDbContext context, int paymentTermDays, string name = "客户甲")
    {
        var partner = TestSupport.NewPartner(name, PartnerType.Customer);
        partner.PaymentTermDays = paymentTermDays;
        context.Partners.Add(partner);
        await context.SaveChangesAsync();
        return partner;
    }

    private static SalesShipment Shipment(
        Guid partnerId, DateTimeOffset orderDate, decimal total, decimal settled, OrderStatus status = OrderStatus.Normal)
        => new()
        {
            Id = Guid.NewGuid(),
            ShipmentNo = Guid.NewGuid().ToString("N")[..8],
            PartnerId = partnerId,
            PartnerName = "客户甲",
            OrderDate = orderDate,
            TotalAmount = total,
            SettledAmount = settled,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static PurchaseReceipt Receipt(Guid partnerId, decimal total, decimal settled)
        => new()
        {
            Id = Guid.NewGuid(),
            ReceiptNo = Guid.NewGuid().ToString("N")[..8],
            PartnerId = partnerId,
            PartnerName = "客户甲",
            OrderDate = BaseDate,
            TotalAmount = total,
            SettledAmount = settled,
            Status = OrderStatus.Normal,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    // ============================== 到期日与逾期（§0.3） ==============================

    [Fact]
    public async Task 账期为0_到期日应等于单据日期且到期当天不算逾期()
    {
        var context = TestSupport.CreateDbContext();
        var customer = await SeedCustomerAsync(context, paymentTermDays: 0);
        context.SalesShipments.Add(Shipment(customer.Id, new DateTimeOffset(2025, 12, 20, 0, 0, 0, TimeSpan.Zero), 100m, 0m));
        await context.SaveChangesAsync();
        var repository = new SettlementQueryRepository(context);

        var (items, total) = await repository.GetReconciliationAsync(
            null, null, false, new DateOnly(2025, 12, 20), 1, 20);

        Assert.Equal(1, total);
        var row = Assert.Single(items);
        Assert.Equal(0, row.PaymentTermDays);
        Assert.Equal(new DateOnly(2025, 12, 20), row.EarliestDueDate);
        Assert.Equal(0, row.MaxOverdueDays);
        Assert.Equal(0, row.OverdueOrderCount);
    }

    [Fact]
    public async Task 账期内_到期日未到_不应逾期()
    {
        var context = TestSupport.CreateDbContext();
        var customer = await SeedCustomerAsync(context, paymentTermDays: 10);
        context.SalesShipments.Add(Shipment(customer.Id, BaseDate, 100m, 0m)); // 12-20 + 10 → 12-30
        await context.SaveChangesAsync();
        var repository = new SettlementQueryRepository(context);

        var (items, _) = await repository.GetReconciliationAsync(null, null, false, Today, 1, 20);

        var row = Assert.Single(items);
        Assert.Equal(new DateOnly(2025, 12, 30), row.EarliestDueDate);
        Assert.Equal(0, row.MaxOverdueDays);
        Assert.Equal(0, row.OverdueOrderCount);
    }

    [Fact]
    public async Task 已过账期_应计算逾期天数与逾期单据数()
    {
        var context = TestSupport.CreateDbContext();
        var customer = await SeedCustomerAsync(context, paymentTermDays: 5);
        context.SalesShipments.Add(Shipment(customer.Id, BaseDate, 100m, 0m)); // 12-20 + 5 → 12-25，今天 12-28
        await context.SaveChangesAsync();
        var repository = new SettlementQueryRepository(context);

        var (items, _) = await repository.GetReconciliationAsync(null, null, false, Today, 1, 20);

        var row = Assert.Single(items);
        Assert.Equal(new DateOnly(2025, 12, 25), row.EarliestDueDate);
        Assert.Equal(3, row.MaxOverdueDays);
        Assert.Equal(1, row.OverdueOrderCount);
    }

    [Fact]
    public async Task 多张未结单据_应取最早到期日与最大逾期天数()
    {
        var context = TestSupport.CreateDbContext();
        var customer = await SeedCustomerAsync(context, paymentTermDays: 5);
        context.SalesShipments.Add(Shipment(customer.Id, new DateTimeOffset(2025, 12, 10, 0, 0, 0, TimeSpan.Zero), 100m, 0m)); // → 12-15
        context.SalesShipments.Add(Shipment(customer.Id, BaseDate, 100m, 0m));                                                // → 12-25
        await context.SaveChangesAsync();
        var repository = new SettlementQueryRepository(context);

        var (items, _) = await repository.GetReconciliationAsync(null, null, false, Today, 1, 20);

        var row = Assert.Single(items);
        Assert.Equal(new DateOnly(2025, 12, 15), row.EarliestDueDate); // 最早到期日
        Assert.Equal(13, row.MaxOverdueDays);                          // 今天 12-28 − 12-15
        Assert.Equal(2, row.OverdueOrderCount);
    }

    [Fact]
    public async Task 已结清单据_不参与逾期判定()
    {
        var context = TestSupport.CreateDbContext();
        var customer = await SeedCustomerAsync(context, paymentTermDays: 5);
        context.SalesShipments.Add(Shipment(customer.Id, new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero), 100m, 100m)); // 已结清
        context.SalesShipments.Add(Shipment(customer.Id, BaseDate, 100m, 0m));                                                 // 未结 → 12-25
        await context.SaveChangesAsync();
        var repository = new SettlementQueryRepository(context);

        var (items, _) = await repository.GetReconciliationAsync(null, null, false, Today, 1, 20);

        var row = Assert.Single(items);
        Assert.Equal(new DateOnly(2025, 12, 25), row.EarliestDueDate);
        Assert.Equal(1, row.OverdueOrderCount);
        Assert.Equal(100m, row.ReceivableAmount);
    }

    [Fact]
    public async Task 已作废单据_不参与逾期判定()
    {
        var context = TestSupport.CreateDbContext();
        var customer = await SeedCustomerAsync(context, paymentTermDays: 5);
        context.SalesShipments.Add(Shipment(
            customer.Id, new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero), 100m, 0m, OrderStatus.Voided));
        await context.SaveChangesAsync();
        var repository = new SettlementQueryRepository(context);

        var (items, _) = await repository.GetReconciliationAsync(null, null, false, Today, 1, 20);

        var row = Assert.Single(items);
        Assert.Null(row.EarliestDueDate);
        Assert.Equal(0, row.MaxOverdueDays);
        Assert.Equal(0, row.OverdueOrderCount);
    }

    [Fact]
    public async Task 只有应付单据_最早到期日应为空()
    {
        var context = TestSupport.CreateDbContext();
        var customer = await SeedCustomerAsync(context, paymentTermDays: 5);
        context.PurchaseReceipts.Add(Receipt(customer.Id, 500m, 0m)); // 应付侧不受账期 / 逾期约束
        await context.SaveChangesAsync();
        var repository = new SettlementQueryRepository(context);

        var (items, _) = await repository.GetReconciliationAsync(null, null, false, Today, 1, 20);

        var row = Assert.Single(items);
        Assert.Equal(0m, row.ReceivableAmount);
        Assert.Equal(500m, row.PayableAmount);
        Assert.Null(row.EarliestDueDate);
        Assert.Equal(0, row.OverdueOrderCount);
    }

    [Fact]
    public async Task 仅看逾期_应只返回存在逾期应收的客户()
    {
        var context = TestSupport.CreateDbContext();
        var overdueCustomer = await SeedCustomerAsync(context, paymentTermDays: 5, name: "逾期客户");
        var safeCustomer = await SeedCustomerAsync(context, paymentTermDays: 30, name: "正常客户");
        context.SalesShipments.Add(Shipment(overdueCustomer.Id, BaseDate, 100m, 0m)); // → 12-25 逾期
        context.SalesShipments.Add(Shipment(safeCustomer.Id, BaseDate, 100m, 0m));    // → 01-19 未到期
        await context.SaveChangesAsync();
        var repository = new SettlementQueryRepository(context);

        var (all, allTotal) = await repository.GetReconciliationAsync(null, null, false, Today, 1, 20);
        Assert.Equal(2, allTotal);

        var (overdueOnly, overdueTotal) = await repository.GetReconciliationAsync(null, null, true, Today, 1, 20);
        Assert.Equal(1, overdueTotal);
        Assert.Equal("逾期客户", Assert.Single(overdueOnly).PartnerName);
    }

    [Fact]
    public async Task 台账查询_应透传逾期筛选与分页并输出派生字段()
    {
        var repository = new FakeSettlementQueryRepository
        {
            ReconciliationItems =
            [
                new ReconciliationItem
                {
                    PartnerId = Guid.NewGuid(),
                    PartnerName = "逾期客户",
                    PartnerType = PartnerType.Customer,
                    ReceivableAmount = 800m,
                    PayableAmount = 0m,
                    UnsettledOrderCount = 1,
                    PaymentTermDays = 5,
                    EarliestDueDate = new DateOnly(2025, 12, 25),
                    MaxOverdueDays = 3,
                    OverdueOrderCount = 1,
                },
            ],
            ReconciliationTotal = 1,
        };
        var handler = new GetReconciliationRequestHandler(repository);

        var result = await handler.HandleAsync(new GetReconciliationRequest { OverdueOnly = true, Page = 1, PageSize = 20 });

        var query = Assert.Single(repository.ReconciliationQueries);
        Assert.True(query.OverdueOnly);
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);

        var row = Assert.Single(result.Items);
        Assert.Equal(5, row.PaymentTermDays);
        Assert.Equal("2025-12-25", row.EarliestDueDate);
        Assert.Equal(3, row.MaxOverdueDays);
        Assert.Equal(1, row.OverdueOrderCount);
    }

    // ============================== 往来单位字段（§0.2，全量覆盖） ==============================

    [Fact]
    public async Task 编辑往来单位_账期与额度应写入()
    {
        var context = TestSupport.CreateDbContext();
        var partner = await SeedAsync(context);
        var handler = new UpdatePartnerRequestHandler(
            new PartnerRepository(context), new StubCurrentUser(Guid.NewGuid()), TestSupport.AuditLogger);

        var result = await handler.HandleAsync(new UpdatePartnerRequest
        {
            Id = partner.Id,
            Type = PartnerType.Customer,
            PaymentTermDays = 30,
            CreditLimit = 5000m,
        });

        Assert.Equal(30, result.PaymentTermDays);
        Assert.Equal(5000m, result.CreditLimit);
        var stored = await context.Partners.SingleAsync(p => p.Id == partner.Id);
        Assert.Equal(30, stored.PaymentTermDays);
        Assert.Equal(5000m, stored.CreditLimit);
    }

    [Fact]
    public async Task 编辑往来单位_未传账期与额度_应按全量覆盖清零()
    {
        var context = TestSupport.CreateDbContext();
        var partner = await SeedAsync(context);
        partner.PaymentTermDays = 30;
        partner.CreditLimit = 5000m;
        await context.SaveChangesAsync();
        var handler = new UpdatePartnerRequestHandler(
            new PartnerRepository(context), new StubCurrentUser(Guid.NewGuid()), TestSupport.AuditLogger);

        _ = await handler.HandleAsync(new UpdatePartnerRequest { Id = partner.Id, Type = PartnerType.Customer });

        var stored = await context.Partners.SingleAsync(p => p.Id == partner.Id);
        Assert.Equal(0, stored.PaymentTermDays); // 缺字段 → 视为清空（0 = 现结）
        Assert.Equal(0m, stored.CreditLimit);    // 缺字段 → 视为清空（0 = 不限）
    }

    [Fact]
    public void 账期与额度_边界应与字段约束同源()
    {
        var validator = new UpdatePartnerRequestValidator();
        const int maxTermDays = PartnerFieldConstraints.PaymentTermDaysMaxValue;
        const decimal maxCreditLimit = PartnerFieldConstraints.CreditLimitMaxValue;

        Assert.True(validator.Validate(Request(paymentTermDays: 0)).IsValid);
        Assert.True(validator.Validate(Request(paymentTermDays: maxTermDays)).IsValid);
        Assert.False(validator.Validate(Request(paymentTermDays: maxTermDays + 1)).IsValid);
        Assert.False(validator.Validate(Request(paymentTermDays: -1)).IsValid);

        Assert.True(validator.Validate(Request(creditLimit: 0m)).IsValid);
        Assert.True(validator.Validate(Request(creditLimit: maxCreditLimit)).IsValid);
        Assert.False(validator.Validate(Request(creditLimit: maxCreditLimit + 0.01m)).IsValid);
        Assert.False(validator.Validate(Request(creditLimit: -0.01m)).IsValid);
    }

    private static UpdatePartnerRequest Request(int paymentTermDays = 0, decimal creditLimit = 0m)
        => new()
        {
            Id = Guid.NewGuid(),
            Type = PartnerType.Customer,
            PaymentTermDays = paymentTermDays,
            CreditLimit = creditLimit,
        };

    [Fact]
    public async Task 未结单据候选_到期日应为单据日期加账期()
    {
        var context = TestSupport.CreateDbContext();
        var customer = await SeedCustomerAsync(context, paymentTermDays: 7);
        context.SalesShipments.Add(Shipment(customer.Id, BaseDate, 100m, 0m)); // 12-20 + 7 → 12-27
        await context.SaveChangesAsync();
        var repository = new SettlementQueryRepository(context);

        var (items, total) = await repository.GetUnsettledAsync(customer.Id, SettlementType.Receipt, 1, 20);

        Assert.Equal(1, total);
        Assert.Equal(new DateOnly(2025, 12, 27), Assert.Single(items).DueDate);
    }

    [Fact]
    public async Task 销售出库详情_到期日应按客户账期推导()
    {
        var context = TestSupport.CreateDbContext();
        var customer = await SeedCustomerAsync(context, paymentTermDays: 10);
        var shipment = new SalesShipment
        {
            Id = Guid.NewGuid(),
            ShipmentNo = Guid.NewGuid().ToString("N")[..8],
            PartnerId = customer.Id,
            PartnerName = "客户甲",
            OrderDate = BaseDate,
            TotalAmount = 100m,
            SettledAmount = 0m,
            Status = OrderStatus.Normal,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        context.SalesShipments.Add(shipment);
        await context.SaveChangesAsync();
        var handler = new GetSalesShipmentByIdRequestHandler(
            new SalesShipmentRepository(context), new PartnerRepository(context));

        var result = await handler.HandleAsync(new GetSalesShipmentByIdRequest { Id = shipment.Id });

        // 单据日期 2025-12-20 + 账期 10 天
        Assert.Equal("2025-12-30", result.DueDate);
    }

    private static async Task<Partner> SeedAsync(AppDbContext context)
    {
        var partner = TestSupport.NewPartner("客户甲", PartnerType.Customer);
        context.Partners.Add(partner);
        await context.SaveChangesAsync();
        return partner;
    }
}
