using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Leads.UpdateLead;

/// <summary>
/// 编辑线索请求格式校验：只做数据格式检查（长度 / 枚举取值）。
/// 存在性与状态流转（终态限制）等查库约束在 Handler 中判断（后端规则 §4.1）。
/// </summary>
public sealed class UpdateLeadRequestValidator : AbstractValidator<UpdateLeadRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateLeadRequestValidator()
    {
        // 路由参数不得从请求体绑定（后端规则 §4.2）：Id 可缺省，Controller 以路由值覆盖，此处仅兜底
        RuleFor(x => x.Id).NotEmpty().WithMessage("线索 id 不能为空");

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
        RuleFor(x => x.Status).Must(s => Enum.IsDefined(s)).WithMessage("线索状态取值非法");
    }
}
