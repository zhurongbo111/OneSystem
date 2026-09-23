using App.Core.Entities;
using App.Core.Features.Quotations.CreateQuotation;
using App.Core.Features.Quotations.GetQuotations;
using App.Core.Features.Quotations.UpdateQuotation;
using App.Infrastructure;

namespace App.Tests;

/// <summary>
/// 报价单（erp-quotation）字段约束一致性测试：
/// 单号 / 备注列长同源 <see cref="OrderFieldConstraints"/>，明细快照列同源 <see cref="ProductFieldConstraints"/>；
/// 有效期边界、明细行数上限、数量 / 单价边界在创建与编辑两处一致；查询关键词不超过单号列长。
/// </summary>
public class QuotationFieldConsistencyTests
{
    private static readonly DateTimeOffset QuotationDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static int GetMaxLength<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetMaxLength()!.Value;

    // ============================== EF 模型 ←→ 常量 ==============================

    [Fact]
    public void EF模型_报价单主表列长度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(OrderFieldConstraints.OrderNoMaxLength, GetMaxLength<Quotation>(dbContext, nameof(Quotation.QuotationNo)));
        Assert.Equal(OrderFieldConstraints.OrderNoMaxLength, GetMaxLength<Quotation>(dbContext, nameof(Quotation.ConvertedOrderNo)));
        Assert.Equal(OrderFieldConstraints.RemarkMaxLength, GetMaxLength<Quotation>(dbContext, nameof(Quotation.Remark)));
        Assert.Equal(PartnerFieldConstraints.NameMaxLength, GetMaxLength<Quotation>(dbContext, nameof(Quotation.PartnerName)));
    }

    [Fact]
    public void EF模型_报价单明细快照列长度_应等于商品字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(ProductFieldConstraints.NameMaxLength, GetMaxLength<QuotationItem>(dbContext, nameof(QuotationItem.ProductName)));
        Assert.Equal(ProductFieldConstraints.UnitMaxLength, GetMaxLength<QuotationItem>(dbContext, nameof(QuotationItem.Unit)));
    }

    // ============================== 有效期边界（创建 / 编辑一致）==============================

    private static CreateQuotationRequest NewCreate(DateOnly? validUntil, int itemCount = 1)
        => new()
        {
            PartnerId = Guid.NewGuid(),
            QuotationDate = QuotationDate,
            ValidUntil = validUntil,
            Items = Enumerable.Range(0, itemCount)
                .Select(_ => new CreateQuotationItem { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 1m })
                .ToList(),
        };

    private static UpdateQuotationRequest NewUpdate(DateOnly? validUntil, int itemCount = 1)
        => new()
        {
            Id = Guid.NewGuid(),
            PartnerId = Guid.NewGuid(),
            QuotationDate = QuotationDate,
            ValidUntil = validUntil,
            Items = Enumerable.Range(0, itemCount)
                .Select(_ => new UpdateQuotationItem { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 1m })
                .ToList(),
        };

    [Fact]
    public void 报价单有效期边界_早于报价日期拒绝_当日与空通过()
    {
        var createValidator = new CreateQuotationRequestValidator();
        var updateValidator = new UpdateQuotationRequestValidator();
        var quotationDay = DateOnly.FromDateTime(QuotationDate.UtcDateTime);

        Assert.False(createValidator.Validate(NewCreate(quotationDay.AddDays(-1))).IsValid);
        Assert.True(createValidator.Validate(NewCreate(quotationDay)).IsValid);
        Assert.True(createValidator.Validate(NewCreate(null)).IsValid);

        Assert.False(updateValidator.Validate(NewUpdate(quotationDay.AddDays(-1))).IsValid);
        Assert.True(updateValidator.Validate(NewUpdate(quotationDay)).IsValid);
        Assert.True(updateValidator.Validate(NewUpdate(null)).IsValid);
    }

    // ============================== 明细行数 / 数量 / 单价边界 ==============================

    [Fact]
    public void 报价单明细行数上限_创建与编辑两处应一致()
    {
        var createValidator = new CreateQuotationRequestValidator();
        var updateValidator = new UpdateQuotationRequestValidator();

        Assert.True(createValidator.Validate(NewCreate(null, OrderFieldConstraints.ItemsMaxCount)).IsValid);
        Assert.False(createValidator.Validate(NewCreate(null, OrderFieldConstraints.ItemsMaxCount + 1)).IsValid);
        Assert.True(updateValidator.Validate(NewUpdate(null, OrderFieldConstraints.ItemsMaxCount)).IsValid);
        Assert.False(updateValidator.Validate(NewUpdate(null, OrderFieldConstraints.ItemsMaxCount + 1)).IsValid);
    }

    [Fact]
    public void 报价单明细数量与单价边界_创建与编辑两处应一致()
    {
        var createValidator = new CreateQuotationRequestValidator();
        var updateValidator = new UpdateQuotationRequestValidator();

        CreateQuotationRequest CreateWith(int quantity, decimal price)
            => new()
            {
                PartnerId = Guid.NewGuid(),
                QuotationDate = QuotationDate,
                Items = new[] { new CreateQuotationItem { ProductId = Guid.NewGuid(), Quantity = quantity, UnitPrice = price } },
            };

        UpdateQuotationRequest UpdateWith(int quantity, decimal price)
            => new()
            {
                Id = Guid.NewGuid(),
                PartnerId = Guid.NewGuid(),
                QuotationDate = QuotationDate,
                Items = new[] { new UpdateQuotationItem { ProductId = Guid.NewGuid(), Quantity = quantity, UnitPrice = price } },
            };

        // 边界值通过：最小 / 最大数量与单价
        Assert.True(createValidator.Validate(CreateWith(ProductFieldConstraints.QuantityMinValue, ProductFieldConstraints.PriceMinValue)).IsValid);
        Assert.True(createValidator.Validate(CreateWith(ProductFieldConstraints.QuantityMaxValue, ProductFieldConstraints.PriceMaxValue)).IsValid);
        Assert.True(updateValidator.Validate(UpdateWith(ProductFieldConstraints.QuantityMinValue, ProductFieldConstraints.PriceMinValue)).IsValid);
        Assert.True(updateValidator.Validate(UpdateWith(ProductFieldConstraints.QuantityMaxValue, ProductFieldConstraints.PriceMaxValue)).IsValid);

        // 越界拒绝：数量 0 / 超上限，单价为负 / 超上限
        Assert.False(createValidator.Validate(CreateWith(ProductFieldConstraints.QuantityMinValue - 1, 1m)).IsValid);
        Assert.False(createValidator.Validate(CreateWith(ProductFieldConstraints.QuantityMaxValue + 1, 1m)).IsValid);
        Assert.False(createValidator.Validate(CreateWith(1, ProductFieldConstraints.PriceMinValue - 0.01m)).IsValid);
        Assert.False(createValidator.Validate(CreateWith(1, ProductFieldConstraints.PriceMaxValue + 1m)).IsValid);
        Assert.False(updateValidator.Validate(UpdateWith(ProductFieldConstraints.QuantityMinValue - 1, 1m)).IsValid);
        Assert.False(updateValidator.Validate(UpdateWith(1, ProductFieldConstraints.PriceMaxValue + 1m)).IsValid);
    }

    // ============================== 查询关键词 ←→ 单号列长 / 状态取值 ==============================

    [Fact]
    public void 报价单查询关键词长度_应不超过单号列长()
    {
        var ok = new string('a', OrderFieldConstraints.KeywordMaxLength);
        var tooLong = new string('a', OrderFieldConstraints.KeywordMaxLength + 1);
        var validator = new GetQuotationsRequestValidator();

        Assert.True(validator.Validate(new GetQuotationsRequest { Page = 1, PageSize = 20, Keyword = ok }).IsValid);
        Assert.False(validator.Validate(new GetQuotationsRequest { Page = 1, PageSize = 20, Keyword = tooLong }).IsValid);
    }

    [Fact]
    public void 报价单状态筛选_非法取值应被拒绝()
    {
        var validator = new GetQuotationsRequestValidator();

        foreach (var status in Enum.GetValues<QuotationStatus>())
        {
            Assert.True(validator.Validate(new GetQuotationsRequest { Page = 1, PageSize = 20, Status = status }).IsValid);
        }

        Assert.False(validator.Validate(new GetQuotationsRequest { Page = 1, PageSize = 20, Status = (QuotationStatus)99 }).IsValid);
    }

    [Fact]
    public void 报价单查询日期范围_结束早于开始应被拒绝()
    {
        var validator = new GetQuotationsRequestValidator();
        var start = QuotationDate.AddDays(1);

        Assert.True(validator.Validate(new GetQuotationsRequest { Page = 1, PageSize = 20, Start = start, End = start }).IsValid);
        Assert.False(validator.Validate(new GetQuotationsRequest { Page = 1, PageSize = 20, Start = start, End = QuotationDate }).IsValid);
    }
}
