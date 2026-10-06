using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Leads;

/// <summary>
/// 线索状态流转规则（specs/043-erp-crm-presale design.md §0.1 / §3.4，**唯一事实源**）：
/// 编辑（`UpdateLead`）与状态流转（`UpdateLeadStatus`）两处共用同一份判据，避免规则分叉。
/// </summary>
internal static class LeadStatusRules
{
    /// <summary>
    /// 校验线索状态变更是否允许，不允许时抛业务异常：
    /// ① 当前为终态（已转化 / 已废弃）→ 40168；
    /// ② 目标为「已转化」→ 40168（只能由转商机产生，须回填商机 id / 单号）。
    /// </summary>
    /// <param name="current">当前状态</param>
    /// <param name="target">目标状态</param>
    public static void EnsureChangeAllowed(LeadStatus current, LeadStatus target)
    {
        if (IsTerminal(current))
        {
            throw new BusinessException(ErrorCode.LeadNotConvertible, "线索已转化或已废弃，不可再改状态");
        }

        if (target == LeadStatus.Converted)
        {
            throw new BusinessException(ErrorCode.LeadNotConvertible, "「已转化」状态请通过转商机产生");
        }
    }

    /// <summary>是否为终态（已转化 / 已废弃）</summary>
    /// <param name="status">线索状态</param>
    public static bool IsTerminal(LeadStatus status) => status is LeadStatus.Converted or LeadStatus.Abandoned;

    /// <summary>
    /// 校验线索是否可转商机：终态（已转化 / 已废弃）抛 40168（design.md §0.2：一条线索只能转一次）。
    /// </summary>
    /// <param name="status">线索当前状态</param>
    public static void EnsureConvertible(LeadStatus status)
    {
        if (IsTerminal(status))
        {
            throw new BusinessException(ErrorCode.LeadNotConvertible, "线索已转化或已废弃，不可再转商机");
        }
    }
}
