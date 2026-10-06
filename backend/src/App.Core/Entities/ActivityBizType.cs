namespace App.Core.Entities;

/// <summary>
/// 跟进活动所属业务类型（specs/043-erp-crm-presale design.md §0.1）。
/// 与 <see cref="Activity.BizId"/> 组合定位活动归属：线索或商机。
/// </summary>
public enum ActivityBizType
{
    /// <summary>线索</summary>
    Lead = 0,

    /// <summary>商机</summary>
    Opportunity = 1,
}
