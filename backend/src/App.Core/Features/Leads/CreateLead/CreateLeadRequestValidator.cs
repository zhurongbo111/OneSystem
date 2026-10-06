using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Leads.CreateLead;

/// <summary>
/// 新增线索请求格式校验：只做数据格式检查（长度 / 枚举取值）。
/// 负责人存在性等查库约束在 Handler 中判断（后端规则 §4.1）。
/// </summary>
public sealed class CreateLeadRequestValidator : AbstractValidator<CreateLeadRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreateLeadRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("线索名称不能为空")
            .MaximumLength(LeadFieldConstraints.NameMaxLength).WithMessage("线索名称超长");
        RuleFor(x => x.Contact).MaximumLength(LeadFieldConstraints.ContactMaxLength).When(x => x.Contact is not null)
            .WithMessage("联系人超长");
        RuleFor(x => x.Phone).MaximumLength(LeadFieldConstraints.PhoneMaxLength).When(x => x.Phone is not null)
            .WithMessage("联系电话超长");
        RuleFor(x => x.Remark).MaximumLength(LeadFieldConstraints.RemarkMaxLength).When(x => x.Remark is not null)
            .WithMessage("备注超长");

        RuleFor(x => x.Source).Must(s => Enum.IsDefined(s)).WithMessage("线索来源取值非法");

        // 新建只允许「新线索 / 跟进中」；「已转化」只能由转商机产生、「已废弃」由状态流转产生
        RuleFor(x => x.Status)
            .Must(s => s is LeadStatus.New or LeadStatus.Following)
            .WithMessage("新建线索的状态只能是新线索或跟进中");
    }
}
