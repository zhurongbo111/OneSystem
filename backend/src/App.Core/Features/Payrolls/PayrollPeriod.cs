namespace App.Core.Features.Payrolls;

/// <summary>
/// 工资单期间文本的**单一来源**（`yyyy-MM`）：审计摘要与差异文本共用，避免各用例各写一套格式。
/// </summary>
internal static class PayrollPeriod
{
    /// <summary>期间文本（yyyy-MM，月份补零）</summary>
    /// <param name="year">年</param>
    /// <param name="month">月</param>
    public static string Text(int year, int month) => $"{year:D4}-{month:D2}";
}
