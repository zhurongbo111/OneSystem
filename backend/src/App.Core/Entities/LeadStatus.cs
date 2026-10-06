namespace App.Core.Entities;

/// <summary>
/// 线索状态（specs/043-erp-crm-presale design.md §0.1，取值与文案为该规格唯一事实源）。
/// 流转约束：<see cref="New"/> → <see cref="Following"/> → <see cref="Converted"/>，
/// 任意非终态可转 <see cref="Abandoned"/>；<see cref="Converted"/> / <see cref="Abandoned"/> 为终态
/// （不可再改状态，也不可再转商机，否则 40168）。
/// </summary>
public enum LeadStatus
{
    /// <summary>新线索</summary>
    New = 0,

    /// <summary>跟进中</summary>
    Following = 1,

    /// <summary>已转化（终态：已转出商机）</summary>
    Converted = 2,

    /// <summary>已废弃（终态）</summary>
    Abandoned = 3,
}
