using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Opportunities;

/// <summary>
/// 商机阶段流转规则（specs/043-erp-crm-presale design.md §0.1 / §3.4，**唯一事实源**）：
/// 编辑（`UpdateOpportunity`）与阶段推进（`UpdateOpportunityStage`）两处共用同一份判据，避免规则分叉。
/// </summary>
internal static class OpportunityStageRules
{
    /// <summary>
    /// 校验商机是否允许变更阶段，不允许时抛 <see cref="ErrorCode.OpportunityClosed"/>（40169）：
    /// 当前阶段为终态（赢单 / 输单）后不可再改阶段。
    /// </summary>
    /// <param name="current">当前阶段</param>
    public static void EnsureStageChangeAllowed(OpportunityStage current)
    {
        if (IsTerminal(current))
        {
            throw new BusinessException(ErrorCode.OpportunityClosed, "商机已赢单或输单，不可再改阶段");
        }
    }

    /// <summary>是否为终态（赢单 / 输单）</summary>
    /// <param name="stage">商机阶段</param>
    public static bool IsTerminal(OpportunityStage stage) => stage is OpportunityStage.Won or OpportunityStage.Lost;
}
