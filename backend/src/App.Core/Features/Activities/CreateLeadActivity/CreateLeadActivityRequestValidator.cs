using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Activities.CreateLeadActivity;

/// <summary>
/// 新增线索跟进活动请求格式校验：只做数据格式检查（id 兜底、枚举取值、内容长度、跟进时间必填）。
/// 线索存在性等查库约束在 Handler 中判断（后端规则 §4.1）。
/// </summary>
public sealed class CreateLeadActivityRequestValidator : AbstractValidator<CreateLeadActivityRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreateLeadActivityRequestValidator()
    {
        RuleFor(x => x.LeadId).NotEmpty().WithMessage("线索 id 不能为空");
        RuleFor(x => x.Type).Must(t => Enum.IsDefined(t)).WithMessage("跟进方式取值非法");
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("跟进内容不能为空")
            .MaximumLength(ActivityFieldConstraints.ContentMaxLength).WithMessage("跟进内容超长");
        RuleFor(x => x.ActivityTime).NotEmpty().WithMessage("跟进时间不能为空");
    }
}
