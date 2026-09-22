using App.Core.Entities;
using App.Core.Features.Invoices.CreateInvoice;
using App.Core.Features.Invoices.ExportInvoices;
using App.Core.Features.Invoices.GetInvoices;
using App.Core.Features.Invoices.GetInvoicableOrders;
using App.Infrastructure;
using App.Infrastructure.Persistence;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// 发票字段约束一致性测试（specs/032-erp-invoice tasks.md 3.7）：
/// ① EF 实际列长 / 精度 == 对应常量；② 查询关键词上限 == 实际匹配列长；
/// ③ 税率区间 / 小数位、金额边界、明细行数与去重规则在登记用例内一致。
/// </summary>
public class InvoiceFieldConsistencyTests
{
    private static int GetMaxLength<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetMaxLength()!.Value;

    // ============================== EF 模型 ←→ 常量 ==============================

    [Fact]
    public void EF模型_Invoices表列长与税率精度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();
        var taxRate = dbContext.Model.FindEntityType(typeof(Invoice))!.FindProperty(nameof(Invoice.TaxRate))!;

        Assert.Equal(InvoiceFieldConstraints.InvoiceNoMaxLength, GetMaxLength<Invoice>(dbContext, nameof(Invoice.InvoiceNo)));
        Assert.Equal(PartnerFieldConstraints.NameMaxLength, GetMaxLength<Invoice>(dbContext, nameof(Invoice.PartnerName)));
        Assert.Equal(OrderFieldConstraints.RemarkMaxLength, GetMaxLength<Invoice>(dbContext, nameof(Invoice.Remark)));
        Assert.Equal(InvoiceFieldConstraints.TaxRatePrecision, taxRate.GetPrecision());
        Assert.Equal(InvoiceFieldConstraints.TaxRateDecimalPlaces, taxRate.GetScale());
    }

    [Fact]
    public void EF模型_InvoiceItems表列长_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(OrderFieldConstraints.OrderNoMaxLength, GetMaxLength<InvoiceItem>(dbContext, nameof(InvoiceItem.OrderNo)));
    }

    // ============================== 查询关键词上限 == 实际匹配列 ==============================

    [Fact]
    public void 发票查询与导出关键词长度_应不超过发票号列长()
    {
        var ok = new string('a', InvoiceFieldConstraints.KeywordMaxLength);
        var tooLong = new string('a', InvoiceFieldConstraints.KeywordMaxLength + 1);

        Assert.True(new GetInvoicesRequestValidator()
            .Validate(new GetInvoicesRequest { Page = 1, PageSize = 20, Keyword = ok }).IsValid);
        Assert.False(new GetInvoicesRequestValidator()
            .Validate(new GetInvoicesRequest { Page = 1, PageSize = 20, Keyword = tooLong }).IsValid);

        Assert.True(new ExportInvoicesRequestValidator()
            .Validate(new ExportInvoicesRequest { Page = 1, PageSize = 20, Keyword = ok }).IsValid);
        Assert.False(new ExportInvoicesRequestValidator()
            .Validate(new ExportInvoicesRequest { Page = 1, PageSize = 20, Keyword = tooLong }).IsValid);
    }

    [Fact]
    public void 可开票候选查询_往来与类型必填_分页越界拒绝()
    {
        Assert.True(new GetInvoicableOrdersRequestValidator()
            .Validate(new GetInvoicableOrdersRequest { PartnerId = Guid.NewGuid(), Type = InvoiceType.Sales, Page = 1, PageSize = 100 }).IsValid);
        Assert.False(new GetInvoicableOrdersRequestValidator()
            .Validate(new GetInvoicableOrdersRequest { PartnerId = Guid.Empty, Type = InvoiceType.Sales }).IsValid);
        Assert.False(new GetInvoicableOrdersRequestValidator()
            .Validate(new GetInvoicableOrdersRequest { PartnerId = Guid.NewGuid(), Type = (InvoiceType)2 }).IsValid);
        Assert.False(new GetInvoicableOrdersRequestValidator()
            .Validate(new GetInvoicableOrdersRequest { PartnerId = Guid.NewGuid(), Type = InvoiceType.Sales, Page = 0 }).IsValid);
        Assert.False(new GetInvoicableOrdersRequestValidator()
            .Validate(new GetInvoicableOrdersRequest { PartnerId = Guid.NewGuid(), Type = InvoiceType.Sales, PageSize = 101 }).IsValid);
    }

    // ============================== 登记用例：税率 / 金额 / 明细 ==============================

    [Fact]
    public void 税率区间与小数位_边界值通过越界拒绝()
    {
        Assert.True(ValidateRate(InvoiceFieldConstraints.TaxRateMinValue));
        Assert.True(ValidateRate(InvoiceFieldConstraints.TaxRateMaxValue));
        Assert.True(ValidateRate(0.1300m));
        Assert.True(ValidateRate(0.1234m));

        Assert.False(ValidateRate(InvoiceFieldConstraints.TaxRateMaxValue + 0.0001m));
        Assert.False(ValidateRate(-0.0001m));
        Assert.False(ValidateRate(0.123456m));
    }

    [Fact]
    public void 不含税金额与明细金额_边界值通过越界拒绝()
    {
        Assert.True(ValidateInvoice(amountExcludingTax: 0.01m));
        Assert.True(ValidateInvoice(amountExcludingTax: ProductFieldConstraints.PriceMaxValue));
        Assert.False(ValidateInvoice(amountExcludingTax: 0m));
        Assert.False(ValidateInvoice(amountExcludingTax: ProductFieldConstraints.PriceMaxValue + 0.01m));

        Assert.True(ValidateInvoice(itemAmount: 0.01m));
        Assert.False(ValidateInvoice(itemAmount: 0m));
        Assert.False(ValidateInvoice(itemAmount: ProductFieldConstraints.PriceMaxValue + 0.01m));
    }

    [Fact]
    public void 关联明细行数与去重_应符合订单域同源约束()
    {
        // 至少 1 行
        Assert.False(ValidateInvoice(items: []));

        // 上限 100 行通过、101 行拒绝（单一来源 OrderFieldConstraints.ItemsMaxCount）
        Assert.True(ValidateInvoice(items: BuildLines(OrderFieldConstraints.ItemsMaxCount)));
        Assert.False(ValidateInvoice(items: BuildLines(OrderFieldConstraints.ItemsMaxCount + 1)));

        // 同一单据（orderType + orderId）不允许重复
        var orderId = Guid.NewGuid();
        Assert.False(ValidateInvoice(items:
        [
            new CreateInvoiceItem { OrderType = SettlementOrderType.SalesOutbound, OrderId = orderId, Amount = 10m },
            new CreateInvoiceItem { OrderType = SettlementOrderType.SalesOutbound, OrderId = orderId, Amount = 20m },
        ]));
    }

    private static List<CreateInvoiceItem> BuildLines(int count)
        => Enumerable.Range(0, count)
            .Select(_ => new CreateInvoiceItem
            {
                OrderType = SettlementOrderType.SalesOutbound,
                OrderId = Guid.NewGuid(),
                Amount = 1m,
            })
            .ToList();

    private static bool ValidateRate(decimal taxRate)
        => new CreateInvoiceRequestValidator().Validate(NewRequest(taxRate: taxRate)).IsValid;

    private static bool ValidateInvoice(decimal amountExcludingTax = 100m, decimal? itemAmount = null, List<CreateInvoiceItem>? items = null)
        => new CreateInvoiceRequestValidator()
            .Validate(NewRequest(amountExcludingTax: amountExcludingTax, itemAmount: itemAmount, items: items))
            .IsValid;

    private static CreateInvoiceRequest NewRequest(
        decimal amountExcludingTax = 100m,
        decimal taxRate = 0.13m,
        decimal? itemAmount = null,
        List<CreateInvoiceItem>? items = null)
        => new()
        {
            InvoiceNo = "INV-1",
            Type = InvoiceType.Sales,
            PartnerId = Guid.NewGuid(),
            InvoiceDate = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero),
            AmountExcludingTax = amountExcludingTax,
            TaxRate = taxRate,
            Items = items ??
            [
                new CreateInvoiceItem
                {
                    OrderType = SettlementOrderType.SalesOutbound,
                    OrderId = Guid.NewGuid(),
                    Amount = itemAmount ?? 100m,
                },
            ],
        };
}