namespace App.Core.Entities;

/// <summary>
/// 会计科目余额方向（借 / 贷）：余额计算与报表取数依据
/// （specs/031-erp-finance-master/design.md §0.1）
/// </summary>
public enum AccountDirection
{
    /// <summary>借</summary>
    Debit = 1,

    /// <summary>贷</summary>
    Credit = 2,
}