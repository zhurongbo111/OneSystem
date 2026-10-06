namespace App.Core.Entities;

/// <summary>
/// 线索来源（specs/043-erp-crm-presale design.md §0.1，取值与文案为该规格唯一事实源）。
/// 仅作登记与筛选维度，不参与任何状态流转判定。
/// </summary>
public enum LeadSource
{
    /// <summary>网站</summary>
    Website = 0,

    /// <summary>电话</summary>
    Phone = 1,

    /// <summary>推荐</summary>
    Referral = 2,

    /// <summary>展会</summary>
    Exhibition = 3,

    /// <summary>其他（默认）</summary>
    Other = 4,
}
