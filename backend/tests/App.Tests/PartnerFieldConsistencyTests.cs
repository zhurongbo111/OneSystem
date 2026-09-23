using App.Core.Entities;
using App.Core.Features.PartnerPrices.CreatePartnerPrice;
using App.Core.Features.PartnerPrices.GetPartnerPrices;
using App.Core.Features.PartnerPrices.UpdatePartnerPrice;
using App.Core.Features.Partners.CreatePartner;
using App.Core.Features.Partners.GetPartners;
using App.Core.Features.Partners.UpdatePartner;
using App.Infrastructure;

namespace App.Tests;

/// <summary>
/// 往来单位字段约束一致性测试：保证"格式校验"与"数据库约束"同源、同一字段在不同用例中规则一致。
/// 守护点：
/// 1. EF 模型实际列长度必须等于 PartnerFieldConstraints 常量；
/// 2. 联系人 / 地址 / 备注 / 名称长度在新增 / 编辑两处一致；
/// 3. 联系电话格式按常量正则校验；
/// 4. 查询关键词长度不超过对应列长度。
/// </summary>
public class PartnerFieldConsistencyTests
{
    private static int GetMaxLength<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetMaxLength()!.Value;

    [Fact]
    public void EF模型_Partners表列长度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(PartnerFieldConstraints.NameMaxLength, GetMaxLength<Partner>(dbContext, nameof(Partner.Name)));
        Assert.Equal(PartnerFieldConstraints.ContactMaxLength, GetMaxLength<Partner>(dbContext, nameof(Partner.Contact)));
        Assert.Equal(PartnerFieldConstraints.PhoneMaxLength, GetMaxLength<Partner>(dbContext, nameof(Partner.Phone)));
        Assert.Equal(PartnerFieldConstraints.AddressMaxLength, GetMaxLength<Partner>(dbContext, nameof(Partner.Address)));
        Assert.Equal(PartnerFieldConstraints.RemarkMaxLength, GetMaxLength<Partner>(dbContext, nameof(Partner.Remark)));
    }

    [Fact]
    public void 联系人_地址_备注长度_新增与编辑应一致()
    {
        var createValidator = new CreatePartnerRequestValidator();
        var updateValidator = new UpdatePartnerRequestValidator();

        var contactOk = new string('联', PartnerFieldConstraints.ContactMaxLength);
        var contactLong = new string('联', PartnerFieldConstraints.ContactMaxLength + 1);
        Assert.True(CreateValid(createValidator, Contact: contactOk));
        Assert.False(CreateValid(createValidator, Contact: contactLong));
        Assert.True(UpdateValid(updateValidator, Contact: contactOk));
        Assert.False(UpdateValid(updateValidator, Contact: contactLong));

        var addressOk = new string('地', PartnerFieldConstraints.AddressMaxLength);
        var addressLong = new string('地', PartnerFieldConstraints.AddressMaxLength + 1);
        Assert.True(CreateValid(createValidator, Address: addressOk));
        Assert.False(CreateValid(createValidator, Address: addressLong));
        Assert.True(UpdateValid(updateValidator, Address: addressOk));
        Assert.False(UpdateValid(updateValidator, Address: addressLong));

        var remarkOk = new string('注', PartnerFieldConstraints.RemarkMaxLength);
        var remarkLong = new string('注', PartnerFieldConstraints.RemarkMaxLength + 1);
        Assert.True(CreateValid(createValidator, Remark: remarkOk));
        Assert.False(CreateValid(createValidator, Remark: remarkLong));
        Assert.True(UpdateValid(updateValidator, Remark: remarkOk));
        Assert.False(UpdateValid(updateValidator, Remark: remarkLong));
    }

    [Fact]
    public void 名称长度_新增应按常量约束校验()
    {
        var validator = new CreatePartnerRequestValidator();

        var ok = new string('名', PartnerFieldConstraints.NameMaxLength);
        var tooLong = new string('名', PartnerFieldConstraints.NameMaxLength + 1);
        Assert.True(validator.Validate(new CreatePartnerRequest { Name = ok, Type = PartnerType.Supplier }).IsValid);
        Assert.False(validator.Validate(new CreatePartnerRequest { Name = tooLong, Type = PartnerType.Supplier }).IsValid);
        Assert.False(validator.Validate(new CreatePartnerRequest { Name = string.Empty, Type = PartnerType.Supplier }).IsValid);
    }

    [Fact]
    public void 联系电话格式_应按常量正则校验()
    {
        var validator = new CreatePartnerRequestValidator();

        Assert.True(CreateValid(validator, Phone: "13800000000"));
        Assert.True(CreateValid(validator, Phone: "19911112222"));
        Assert.False(CreateValid(validator, Phone: "12800000000")); // 第二位必须 3-9
        Assert.False(CreateValid(validator, Phone: "1380000000")); // 10 位
        Assert.False(CreateValid(validator, Phone: "138000000001")); // 12 位
        Assert.False(CreateValid(validator, Phone: "23800000000")); // 非 1 开头
    }

    [Fact]
    public void 查询关键词长度_应不超过对应列长度()
    {
        var ok = new string('a', PartnerFieldConstraints.KeywordMaxLength);
        var tooLong = new string('a', PartnerFieldConstraints.KeywordMaxLength + 1);

        Assert.True(new GetPartnersRequestValidator().Validate(new GetPartnersRequest { Page = 1, PageSize = 20, Keyword = ok }).IsValid);
        Assert.False(new GetPartnersRequestValidator().Validate(new GetPartnersRequest { Page = 1, PageSize = 20, Keyword = tooLong }).IsValid);
    }

    [Fact]
    public void 协议价与信用额度_上界应与商品价格口径同源()
    {
        // 精度 numeric(18,2) 由 EF 配置给出，InMemory 提供程序无关系型元数据，故此处守护「口径同源」：
        // 额度上界直接引用商品价格上界常量，协议价上下界同样取自同一来源
        Assert.Equal(ProductFieldConstraints.PriceMaxValue, PartnerFieldConstraints.CreditLimitMaxValue);

        Assert.True(UpdateValid(new UpdatePartnerRequestValidator(), CreditLimit: ProductFieldConstraints.PriceMaxValue));
        Assert.False(UpdateValid(new UpdatePartnerRequestValidator(), CreditLimit: ProductFieldConstraints.PriceMaxValue + 0.01m));
        // 新增侧与编辑侧同口径（036 §3.5：新增请求同样携带额度）
        Assert.True(CreateValid(new CreatePartnerRequestValidator(), CreditLimit: ProductFieldConstraints.PriceMaxValue));
        Assert.False(CreateValid(new CreatePartnerRequestValidator(), CreditLimit: ProductFieldConstraints.PriceMaxValue + 0.01m));
    }

    [Fact]
    public void 协议价备注长度_应与备注常量一致()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(
            OrderFieldConstraints.RemarkMaxLength,
            GetMaxLength<PartnerPrice>(dbContext, nameof(PartnerPrice.Remark)));
    }

    [Fact]
    public void 协议价单价区间_新增与编辑应与商品价格同源()
    {
        var createValidator = new CreatePartnerPriceRequestValidator();
        var updateValidator = new UpdatePartnerPriceRequestValidator();
        var min = ProductFieldConstraints.PriceMinValue;
        var max = ProductFieldConstraints.PriceMaxValue;

        Assert.True(createValidator.Validate(CreatePriceRequest(min)).IsValid);
        Assert.True(createValidator.Validate(CreatePriceRequest(max)).IsValid);
        Assert.False(createValidator.Validate(CreatePriceRequest(max + 0.01m)).IsValid);
        Assert.False(createValidator.Validate(CreatePriceRequest(min - 0.01m)).IsValid);

        Assert.True(updateValidator.Validate(UpdatePriceRequest(min)).IsValid);
        Assert.True(updateValidator.Validate(UpdatePriceRequest(max)).IsValid);
        Assert.False(updateValidator.Validate(UpdatePriceRequest(max + 0.01m)).IsValid);
        Assert.False(updateValidator.Validate(UpdatePriceRequest(min - 0.01m)).IsValid);
    }

    [Fact]
    public void 账期天数上限_应取自单一常量()
    {
        var validator = new UpdatePartnerRequestValidator();

        var result = validator.Validate(new UpdatePartnerRequest
        {
            Id = Guid.NewGuid(),
            Type = PartnerType.Supplier,
            PaymentTermDays = PartnerFieldConstraints.PaymentTermDaysMaxValue + 1,
        });

        Assert.False(result.IsValid);
        Assert.Contains(
            PartnerFieldConstraints.PaymentTermDaysMaxValue.ToString(),
            string.Join('|', result.Errors.Select(e => e.ErrorMessage)));

        // 新增侧同口径：上界取自同一常量，越界同样拒绝
        var createValidator = new CreatePartnerRequestValidator();
        Assert.True(CreateValid(createValidator, PaymentTermDays: PartnerFieldConstraints.PaymentTermDaysMaxValue));
        Assert.False(CreateValid(createValidator, PaymentTermDays: PartnerFieldConstraints.PaymentTermDaysMaxValue + 1));
    }

    [Fact]
    public void 客户价查询关键词长度_应不超过对应列长度()
    {
        var ok = new string('a', PartnerFieldConstraints.KeywordMaxLength);
        var tooLong = new string('a', PartnerFieldConstraints.KeywordMaxLength + 1);
        var validator = new GetPartnerPricesRequestValidator();

        Assert.True(validator.Validate(new GetPartnerPricesRequest { Keyword = ok }).IsValid);
        Assert.False(validator.Validate(new GetPartnerPricesRequest { Keyword = tooLong }).IsValid);
    }

    private static CreatePartnerPriceRequest CreatePriceRequest(decimal price)
        => new() { PartnerId = Guid.NewGuid(), ProductId = Guid.NewGuid(), Price = price };

    private static UpdatePartnerPriceRequest UpdatePriceRequest(decimal price)
        => new() { Id = Guid.NewGuid(), Price = price };

    private static bool CreateValid(CreatePartnerRequestValidator validator, string? Contact = null, string? Phone = null, string? Address = null, string? Remark = null, int? PaymentTermDays = null, decimal? CreditLimit = null)
        => validator.Validate(new CreatePartnerRequest
        {
            Name = "供应商A",
            Type = PartnerType.Supplier,
            Contact = Contact,
            Phone = Phone,
            Address = Address,
            Remark = Remark,
            PaymentTermDays = PaymentTermDays ?? 0,
            CreditLimit = CreditLimit ?? 0m,
        }).IsValid;

    private static bool UpdateValid(UpdatePartnerRequestValidator validator, string? Contact = null, string? Phone = null, string? Address = null, string? Remark = null, decimal? CreditLimit = null)
        => validator.Validate(new UpdatePartnerRequest
        {
            Id = Guid.NewGuid(),
            Type = PartnerType.Supplier,
            Contact = Contact,
            Phone = Phone,
            Address = Address,
            Remark = Remark,
            CreditLimit = CreditLimit ?? 0m,
        }).IsValid;
}
