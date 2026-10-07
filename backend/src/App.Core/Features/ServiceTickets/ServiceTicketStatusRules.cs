using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.ServiceTickets;

/// <summary>
/// 服务工单状态流转规则（specs/045-erp-crm-service design.md §0.1 / §3.4，**唯一事实源**）：
/// 状态流转端点（`UpdateServiceTicketStatus`）与「已关闭不可编辑」判据（`UpdateServiceTicket` / `AssignServiceTicket`）共用同一份白名单，
/// 避免规则分叉。
/// </summary>
internal static class ServiceTicketStatusRules
{
    /// <summary>允许的状态流转白名单（其余组合一律 40172）</summary>
    private static readonly Dictionary<TicketStatus, TicketStatus[]> _allowed =
        new()
        {
            [TicketStatus.Pending] = [TicketStatus.Processing, TicketStatus.Resolved, TicketStatus.Closed],
            [TicketStatus.Processing] = [TicketStatus.Resolved, TicketStatus.Closed],
            [TicketStatus.Resolved] = [TicketStatus.Closed, TicketStatus.Processing],
            [TicketStatus.Closed] = [],
        };

    /// <summary>
    /// 校验状态流转是否允许，不允许（含已关闭为终态）时抛 <see cref="ErrorCode.TicketStateInvalid"/>。
    /// </summary>
    /// <param name="current">当前状态</param>
    /// <param name="target">目标状态</param>
    public static void EnsureTransitionAllowed(TicketStatus current, TicketStatus target)
    {
        if (!_allowed.TryGetValue(current, out var targets) || !targets.Contains(target))
        {
            throw new BusinessException(
                ErrorCode.TicketStateInvalid,
                $"工单当前状态为{Audit.AuditText.TicketStatus(current)}，不允许变更为{Audit.AuditText.TicketStatus(target)}");
        }
    }

    /// <summary>
    /// 校验工单是否可编辑 / 可指派：已关闭为终态（归档），抛 <see cref="ErrorCode.TicketStateInvalid"/>。
    /// </summary>
    /// <param name="status">工单当前状态</param>
    public static void EnsureNotClosed(TicketStatus status)
    {
        if (status == TicketStatus.Closed)
        {
            throw new BusinessException(ErrorCode.TicketStateInvalid, "工单已关闭（终态），不可编辑");
        }
    }
}
