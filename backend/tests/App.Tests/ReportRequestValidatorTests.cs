using App.Core.Entities;
using App.Core.Features.Reports;
using App.Core.Features.Reports.GetInventoryFlow;
using App.Core.Features.Reports.GetPurchaseSummary;
using App.Core.Features.Reports.GetSalesSummary;
using App.Core.Features.Reports.GetStockBalance;

namespace App.Tests;

/// <summary>
/// 报表请求格式校验测试：期间必填与先后关系、期间上限（同源常量）、分组维度、分页边界、关键词长度。
/// </summary>
public class ReportRequestValidatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    // ============================== 期间必填与先后关系 ==============================

    [Fact]
    public void 进销存报表_期间必填()
    {
        var validator = new GetInventoryFlowRequestValidator();

        Assert.False(validator.Validate(new GetInventoryFlowRequest()).IsValid);
        Assert.False(validator.Validate(new GetInventoryFlowRequest { Start = Start }).IsValid);
        Assert.False(validator.Validate(new GetInventoryFlowRequest { End = Start.AddDays(1) }).IsValid);
        Assert.True(validator.Validate(new GetInventoryFlowRequest { Start = Start, End = Start.AddDays(1) }).IsValid);
    }

    [Fact]
    public void 进销存报表_开始日期不得晚于或等于结束日期()
    {
        var validator = new GetInventoryFlowRequestValidator();

        Assert.False(validator.Validate(new GetInventoryFlowRequest { Start = Start, End = Start }).IsValid);
        Assert.False(validator.Validate(new GetInventoryFlowRequest { Start = Start.AddDays(1), End = Start }).IsValid);
        Assert.True(validator.Validate(new GetInventoryFlowRequest { Start = Start, End = Start.AddDays(1) }).IsValid);
    }

    // ============================== 期间上限（常量同源）==============================

    [Fact]
    public void 期间上限_边界内通过且超出拒绝()
    {
        var validator = new GetInventoryFlowRequestValidator();
        var maxEnd = Start.AddDays(ReportFieldConstraints.MaxRangeDays);

        Assert.True(validator.Validate(new GetInventoryFlowRequest { Start = Start, End = maxEnd }).IsValid);
        Assert.False(validator.Validate(new GetInventoryFlowRequest { Start = Start, End = maxEnd.AddDays(1) }).IsValid);
    }

    [Fact]
    public void 期间上限_采购销售汇总与进销存报表应同源一致()
    {
        var maxEnd = Start.AddDays(ReportFieldConstraints.MaxRangeDays);
        var exceededEnd = maxEnd.AddDays(1);

        Assert.True(new GetPurchaseSummaryRequestValidator()
            .Validate(new GetPurchaseSummaryRequest { Start = Start, End = maxEnd }).IsValid);
        Assert.False(new GetPurchaseSummaryRequestValidator()
            .Validate(new GetPurchaseSummaryRequest { Start = Start, End = exceededEnd }).IsValid);

        Assert.True(new GetSalesSummaryRequestValidator()
            .Validate(new GetSalesSummaryRequest { Start = Start, End = maxEnd }).IsValid);
        Assert.False(new GetSalesSummaryRequestValidator()
            .Validate(new GetSalesSummaryRequest { Start = Start, End = exceededEnd }).IsValid);

        Assert.False(new GetInventoryFlowRequestValidator()
            .Validate(new GetInventoryFlowRequest { Start = Start, End = exceededEnd }).IsValid);
    }

    [Fact]
    public void 汇总报表_期间必填且开始须早于结束()
    {
        var purchase = new GetPurchaseSummaryRequestValidator();
        var sales = new GetSalesSummaryRequestValidator();

        Assert.False(purchase.Validate(new GetPurchaseSummaryRequest()).IsValid);
        Assert.False(sales.Validate(new GetSalesSummaryRequest()).IsValid);
        Assert.False(purchase.Validate(new GetPurchaseSummaryRequest { Start = Start, End = Start }).IsValid);
        Assert.False(sales.Validate(new GetSalesSummaryRequest { Start = Start, End = Start }).IsValid);
        Assert.True(purchase.Validate(new GetPurchaseSummaryRequest { Start = Start, End = Start.AddDays(1) }).IsValid);
        Assert.True(sales.Validate(new GetSalesSummaryRequest { Start = Start, End = Start.AddDays(1) }).IsValid);
    }

    // ============================== 分组维度 ==============================

    [Fact]
    public void 汇总报表_默认按往来单位且非法分组维度应拒绝()
    {
        Assert.True(new GetPurchaseSummaryRequestValidator()
            .Validate(new GetPurchaseSummaryRequest { Start = Start, End = Start.AddDays(1) }).IsValid);
        Assert.True(new GetSalesSummaryRequestValidator()
            .Validate(new GetSalesSummaryRequest { Start = Start, End = Start.AddDays(1) }).IsValid);

        var illegal = (SummaryGroupBy)99;
        Assert.False(new GetPurchaseSummaryRequestValidator()
            .Validate(new GetPurchaseSummaryRequest { Start = Start, End = Start.AddDays(1), GroupBy = illegal }).IsValid);
        Assert.False(new GetSalesSummaryRequestValidator()
            .Validate(new GetSalesSummaryRequest { Start = Start, End = Start.AddDays(1), GroupBy = illegal }).IsValid);
    }

    // ============================== 分页与关键词 ==============================

    [Theory]
    [InlineData(0, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 100, true)]
    [InlineData(1, 101, false)]
    public void 分页边界_四个报表请求应一致(int page, int pageSize, bool expected)
    {
        Assert.Equal(expected, new GetInventoryFlowRequestValidator()
            .Validate(new GetInventoryFlowRequest { Start = Start, End = Start.AddDays(1), Page = page, PageSize = pageSize }).IsValid);
        Assert.Equal(expected, new GetStockBalanceRequestValidator()
            .Validate(new GetStockBalanceRequest { Page = page, PageSize = pageSize }).IsValid);
        Assert.Equal(expected, new GetPurchaseSummaryRequestValidator()
            .Validate(new GetPurchaseSummaryRequest { Start = Start, End = Start.AddDays(1), Page = page, PageSize = pageSize }).IsValid);
        Assert.Equal(expected, new GetSalesSummaryRequestValidator()
            .Validate(new GetSalesSummaryRequest { Start = Start, End = Start.AddDays(1), Page = page, PageSize = pageSize }).IsValid);
    }

    [Fact]
    public void 库存余额表_关键词长度应不超过商品列长()
    {
        var ok = new string('a', ProductFieldConstraints.KeywordMaxLength);
        var tooLong = new string('a', ProductFieldConstraints.KeywordMaxLength + 1);
        var validator = new GetStockBalanceRequestValidator();

        Assert.True(validator.Validate(new GetStockBalanceRequest { Keyword = ok }).IsValid);
        Assert.False(validator.Validate(new GetStockBalanceRequest { Keyword = tooLong }).IsValid);
    }
}
