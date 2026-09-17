namespace App.Core.Features.Reports;

/// <summary>
/// 报表域字段约束的**单一来源**（本域新增，无既有同源常量可引用）：
/// 期间上限由 4 个报表用例的 RequestValidator 统一引用，禁止各处硬编码，
/// 口径见 specs/025-erp-report/design.md §3.5。
/// </summary>
public static class ReportFieldConstraints
{
    /// <summary>
    /// 单次查询期间上限（天）：防止误传超大区间拖垮跨表聚合；如需年度以上报表另行放开（design.md §5）。
    /// </summary>
    public const int MaxRangeDays = 366;
}
