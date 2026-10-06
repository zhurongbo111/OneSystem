using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Approvals.ApproveOrder;

/// <summary>
/// 审批通过请求格式校验：审批意见可空且不超过备注列长（常量取 <see cref="OrderFieldConstraints"/>，design.md §3.6）
/// </summary>
public sealed class ApproveOrderRequestValidator : AbstractValidator<ApproveOrderRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public ApproveOrderRequestValidator()
    {
        RuleFor(x => x.Remark)
            .MaximumLength(OrderFieldConstraints.RemarkMaxLength)
            .WithMessage($"审批意见不能超过 {OrderFieldConstraints.RemarkMaxLength} 个字符")
            .When(x => x.Remark is not null);
    }
}
