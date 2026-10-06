using FluentValidation;

namespace App.Core.Features.Opportunities.UpdateOpportunityStage;

/// <summary>
/// 商机阶段推进请求格式校验：只做数据格式检查（id 兜底、阶段枚举取值）。
/// 终态限制等业务约束在 Handler 中判断（后端规则 §4.1）。
/// </summary>
public sealed class UpdateOpportunityStageRequestValidator : AbstractValidator<UpdateOpportunityStageRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateOpportunityStageRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("商机 id 不能为空");
        RuleFor(x => x.Stage).Must(s => Enum.IsDefined(s)).WithMessage("商机阶段取值非法");
    }
}
