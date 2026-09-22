namespace App.Core.Entities;

/// <summary>
/// 会计科目类别：报表按类别汇总，与余额方向相互独立
/// （specs/031-erp-finance-master/design.md §0.1）
/// </summary>
public enum AccountCategory
{
    /// <summary>资产</summary>
    Asset = 1,

    /// <summary>负债</summary>
    Liability = 2,

    /// <summary>所有者权益</summary>
    Equity = 3,

    /// <summary>成本</summary>
    Cost = 4,

    /// <summary>损益</summary>
    ProfitLoss = 5,
}