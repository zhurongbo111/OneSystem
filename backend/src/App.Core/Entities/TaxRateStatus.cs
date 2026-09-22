namespace App.Core.Entities;

/// <summary>
/// 税率状态：停用税率不可被新发票 / 凭证引用，历史数据保留
/// （specs/031-erp-finance-master/design.md §0.1）
/// </summary>
public enum TaxRateStatus
{
    /// <summary>停用</summary>
    Disabled = 0,

    /// <summary>启用</summary>
    Enabled = 1,
}