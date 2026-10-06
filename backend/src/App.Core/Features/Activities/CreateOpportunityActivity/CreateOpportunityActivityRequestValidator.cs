using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Activities.CreateOpportunityActivity;

/// <summary>
/// 新增商机跟进活动请求格式校验：只做数据格式检查（id 兜底、枚举取值、内容长度、跟进时间必填）。
/// 商机存在性等查库约束在 Handler 中判断（后端规则 §4.1）。
/// </summary>
public sealed class CreateOpportunityActivityRequestValidator : AbstractValidator<CreateOpportunityActivityRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreateOpportunityActivityRequestValidator()
    {
        RuleFor(x => x.OpportunityId).NotEmpty().WithMessage("商机 id 不能为空");
        RuleFor(x => x.Type).Must(t => Enum.IsDefined(t)).WithMessage("跟进方式取值非法");
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("跟进内容不能为空")
            .MaximumLength(ActivityFieldConstraints.ContentMaxLength).WithMessage("跟进内容超长");
        RuleFor(x => x.ActivityTime).NotEmpty().WithMessage("跟进时间不能为空");
    }
}
