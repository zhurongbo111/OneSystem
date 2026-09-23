using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Partners.UpdatePartner;

/// <summary>
/// 编辑往来单位请求格式校验：只做数据格式检查（规则与 CreatePartner 同源，去掉 name）。
/// 长度 / 取值边界统一取自 PartnerFieldConstraints（与 EF 配置一致，禁止硬编码）。
/// </summary>
public sealed class UpdatePartnerRequestValidator : AbstractValidator<UpdatePartnerRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdatePartnerRequestValidator()
    {
        RuleFor(x => x.Type)
            .Must(t => t is PartnerType.Supplier or PartnerType.Customer or PartnerType.Both)
            .WithMessage("单位类型必须为 1（供应商）/ 2（客户）/ 3（两者）");

        RuleFor(x => x.Contact)
            .MaximumLength(PartnerFieldConstraints.ContactMaxLength)
            .WithMessage($"联系人长度不能超过 {PartnerFieldConstraints.ContactMaxLength} 个字符")
            .When(x => x.Contact is not null);

        RuleFor(x => x.Phone)
            .MaximumLength(PartnerFieldConstraints.PhoneMaxLength)
            .Matches(PartnerFieldConstraints.PhonePattern).WithMessage("联系电话格式不正确")
            .When(x => x.Phone is not null);

        RuleFor(x => x.Address)
            .MaximumLength(PartnerFieldConstraints.AddressMaxLength)
            .WithMessage($"地址长度不能超过 {PartnerFieldConstraints.AddressMaxLength} 个字符")
            .When(x => x.Address is not null);

        RuleFor(x => x.Remark)
            .MaximumLength(PartnerFieldConstraints.RemarkMaxLength)
            .WithMessage($"备注长度不能超过 {PartnerFieldConstraints.RemarkMaxLength} 个字符")
            .When(x => x.Remark is not null);

        // 账期与信用额度（036-erp-partner-price）：0 分别表示现结与不限，上限取自单一来源常量
        RuleFor(x => x.PaymentTermDays)
            .InclusiveBetween(0, PartnerFieldConstraints.PaymentTermDaysMaxValue)
            .WithMessage($"账期天数必须在 0 到 {PartnerFieldConstraints.PaymentTermDaysMaxValue} 之间");

        RuleFor(x => x.CreditLimit)
            .InclusiveBetween(0, PartnerFieldConstraints.CreditLimitMaxValue)
            .WithMessage($"信用额度必须在 0 到 {PartnerFieldConstraints.CreditLimitMaxValue} 之间");
    }
}
