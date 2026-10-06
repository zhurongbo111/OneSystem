namespace App.Core.Entities;

/// <summary>
/// 跟进活动类型（specs/043-erp-crm-presale design.md §0.1，取值与文案为该规格唯一事实源）。
/// </summary>
public enum ActivityType
{
    /// <summary>电话</summary>
    Call = 0,

    /// <summary>拜访</summary>
    Visit = 1,

    /// <summary>邮件</summary>
    Email = 2,

    /// <summary>其他</summary>
    Other = 3,
}
