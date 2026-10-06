using FluentValidation;

namespace App.Core.Features.Leads.UpdateLeadStatus;

/// <summary>
/// 线索状态流转请求格式校验：只做数据格式检查（id 兜底、状态枚举取值）。
/// 终态限制等业务约束在 Handler 中判断（后端规则 §4.1）。
/// </summary>
public sealed class UpdateLeadStatusRequestValidator : AbstractValidator<UpdateLeadStatusRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateLeadStatusRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("线索 id 不能为空");
        RuleFor(x => x.Status).Must(s => Enum.IsDefined(s)).WithMessage("线索状态取值非法");
    }
}
