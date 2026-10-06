namespace App.Core.Entities;

/// <summary>
/// 商机阶段（specs/043-erp-crm-presale design.md §0.1，取值与文案为该规格唯一事实源）。
/// 阶段顺序推进；<see cref="Won"/> / <see cref="Lost"/> 为终态（终态后不可再改阶段，否则 40169）。
/// </summary>
public enum OpportunityStage
{
    /// <summary>初步接洽（默认）</summary>
    Initial = 0,

    /// <summary>需求确认</summary>
    Requirement = 1,

    /// <summary>方案报价</summary>
    Proposal = 2,

    /// <summary>谈判</summary>
    Negotiation = 3,

    /// <summary>赢单（终态）</summary>
    Won = 4,

    /// <summary>输单（终态）</summary>
    Lost = 5,
}
