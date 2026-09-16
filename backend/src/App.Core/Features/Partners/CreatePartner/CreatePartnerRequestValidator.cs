using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Partners.CreatePartner;

/// <summary>
/// 新增往来单位请求格式校验：只做数据格式检查；名称是否已存在等查库约束在 Handler 内。
/// 长度 / 取值边界统一取自 PartnerFieldConstraints（与 EF 配置一致，禁止硬编码）。
/// </summary>
public sealed class CreatePartnerRequestValidator : AbstractValidator<CreatePartnerRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreatePartnerRequestValidator()
    {
        // 入参去首尾空白在 Handler 内统一处理（与 user-management 一致），此处只做格式校验
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("单位名称不能为空")
            .Length(PartnerFieldConstraints.NameMinLength, PartnerFieldConstraints.NameMaxLength)
            .WithMessage($"单位名称长度必须在 {PartnerFieldConstraints.NameMinLength} 到 {PartnerFieldConstraints.NameMaxLength} 之间");

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
    }
}
