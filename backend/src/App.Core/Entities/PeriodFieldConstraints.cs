namespace App.Core.Entities;

/// <summary>
/// 会计期间字段约束的**单一来源**：各 <c>RequestValidator</c> 与列表筛选均引用本类常量，
/// 禁止硬编码（specs/033-erp-general-ledger/design.md §2.5）。
/// </summary>
public static class PeriodFieldConstraints
{
    /// <summary>年份下界</summary>
    public const int YearMinValue = 2000;

    /// <summary>年份上界</summary>
    public const int YearMaxValue = 9999;

    /// <summary>月份下界</summary>
    public const int MonthMinValue = 1;

    /// <summary>月份上界</summary>
    public const int MonthMaxValue = 12;
}
