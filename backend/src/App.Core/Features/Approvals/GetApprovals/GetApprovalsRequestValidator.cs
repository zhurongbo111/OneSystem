using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Approvals.GetApprovals;

/// <summary>
/// 审批记录分页查询请求格式校验：只做数据格式检查（design.md §3.6）
/// </summary>
public sealed class GetApprovalsRequestValidator : AbstractValidator<GetApprovalsRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetApprovalsRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("页码必须从 1 开始");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        RuleFor(x => x.Status)
            .Must(s => s is null
                or ApprovalStatus.Pending or ApprovalStatus.Approved
                or ApprovalStatus.Rejected or ApprovalStatus.Withdrawn)
            .WithMessage("审批状态取值非法");

        RuleFor(x => x.OrderType)
            .Must(t => t is null
                or SettlementOrderType.PurchaseInbound or SettlementOrderType.SalesOutbound
                or SettlementOrderType.PurchaseReturn or SettlementOrderType.SalesReturn)
            .WithMessage("单据类型取值非法");

        // 时间范围闭区间：两者都传时 start <= end（跨字段校验用匿名类型组合）
        RuleFor(x => new { x.Start, x.End })
            .Must(v => v.Start is null || v.End is null || v.End >= v.Start)
            .WithMessage("结束时间不能早于开始时间");
    }
}
