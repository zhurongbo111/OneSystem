using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Approvals.RejectApproval;

/// <summary>
/// 审批驳回请求格式校验：审批意见必填且不超过备注列长（常量取 <see cref="OrderFieldConstraints"/>，design.md §3.6）
/// </summary>
public sealed class RejectApprovalRequestValidator : AbstractValidator<RejectApprovalRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public RejectApprovalRequestValidator()
    {
        RuleFor(x => x.Remark)
            .NotEmpty()
            .WithMessage("驳回时必须填写审批意见")
            .MaximumLength(OrderFieldConstraints.RemarkMaxLength)
            .WithMessage($"审批意见不能超过 {OrderFieldConstraints.RemarkMaxLength} 个字符");
    }
}
