using App.Core.Entities;
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

    private static bool CreateValid(CreatePartnerRequestValidator validator, string? Contact = null, string? Phone = null, string? Address = null, string? Remark = null)
        => validator.Validate(new CreatePartnerRequest
        {
            Name = "供应商A",
            Type = PartnerType.Supplier,
            Contact = Contact,
            Phone = Phone,
            Address = Address,
            Remark = Remark,
        }).IsValid;

    private static bool UpdateValid(UpdatePartnerRequestValidator validator, string? Contact = null, string? Phone = null, string? Address = null, string? Remark = null)
        => validator.Validate(new UpdatePartnerRequest
        {
            Id = Guid.NewGuid(),
            Type = PartnerType.Supplier,
            Contact = Contact,
            Phone = Phone,
            Address = Address,
            Remark = Remark,
        }).IsValid;
}
