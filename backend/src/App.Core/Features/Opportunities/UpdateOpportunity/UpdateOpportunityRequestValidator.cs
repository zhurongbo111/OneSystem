using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Opportunities.UpdateOpportunity;

/// <summary>
/// 编辑商机请求格式校验：只做数据格式检查（长度 / 枚举取值 / 金额区间）。
/// 存在性与阶段流转（终态限制）等查库约束在 Handler 中判断（后端规则 §4.1）。
/// </summary>
public sealed class UpdateOpportunityRequestValidator : AbstractValidator<UpdateOpportunityRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateOpportunityRequestValidator()
    {
        // 路由参数不得从请求体绑定（后端规则 §4.2）：Id 可缺省，Controller 以路由值覆盖，此处仅兜底
        RuleFor(x => x.Id).NotEmpty().WithMessage("商机 id 不能为空");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("商机名称不能为空")
            .MaximumLength(OpportunityFieldConstraints.NameMaxLength).WithMessage("商机名称超长");
        RuleFor(x => x.Remark).MaximumLength(OpportunityFieldConstraints.RemarkMaxLength).When(x => x.Remark is not null)
            .WithMessage("备注超长");

        RuleFor(x => x.Amount)
            .InclusiveBetween(0m, OpportunityFieldConstraints.AmountMaxValue)
            .WithMessage("预计金额必须在 0 到允许上限之间");

        RuleFor(x => x.Stage).Must(s => Enum.IsDefined(s)).WithMessage("商机阶段取值非法");
    }
}
