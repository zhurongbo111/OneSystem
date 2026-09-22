using System.Globalization;

namespace App.Core.Audit;

/// <summary>
/// 审计摘要 / 差异文本格式化辅助：把散落各域的格式统一收在一处
/// （<c>specs/029-erp-audit-log/design.md</c> §0.2：摘要金额两位小数、数量去尾零、日期 yyyy-MM-dd、集合用顿号连接）。
/// 所有方法无副作用、纯字符串格式化。
/// </summary>
public static class AuditSummary
{
    /// <summary>空值占位：详情页的空态 "-"</summary>
    public const string Empty = "-";

    /// <summary>金额格式（两位小数，摘要与差异一致）</summary>
    public static string Money(decimal? value)
        => value?.ToString("0.00", CultureInfo.InvariantCulture) ?? Empty;

    /// <summary>数量格式（去尾零）</summary>
    public static string Quantity(decimal? value)
        => value?.ToString("0.##", CultureInfo.InvariantCulture) ?? Empty;

    /// <summary>整数数量格式</summary>
    public static string Count(int? value)
        => value?.ToString(CultureInfo.InvariantCulture) ?? Empty;

    /// <summary>税率百分比格式（去尾零，如 13 / 13.5 / 9）</summary>
    public static string Rate(decimal? value)
        => value?.ToString("0.####", CultureInfo.InvariantCulture) ?? Empty;

    /// <summary>文本格式（空 / 空白均为占位符）</summary>
    public static string Text(string? value)
        => string.IsNullOrWhiteSpace(value) ? Empty : value.Trim();

    /// <summary>日期格式（yyyy-MM-dd）</summary>
    public static string Date(DateTimeOffset? value)
        => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? Empty;

    /// <summary>纯日期格式（yyyy-MM-dd，DateOnly 字段用：入职 / 离职日期）</summary>
    public static string Date(DateOnly? value)
        => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? Empty;

    /// <summary>
    /// 集合快照文本（如角色集合、单据明细编号集合）；空集合返回占位符
    /// </summary>
    /// <param name="values">集合元素</param>
    /// <param name="separator">分隔符，默认中文顿号</param>
    public static string Join(IEnumerable<string>? values, string separator = "、")
    {
        var parts = (values ?? []).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToList();
        return parts.Count == 0 ? Empty : string.Join(separator, parts);
    }

    /// <summary>
    /// 集合差异文本：新增项前缀 <c>+</c>、移除项前缀 <c>-</c>，按「先增后减」拼接；无差异返回占位符
    /// </summary>
    /// <param name="before">变更前的集合</param>
    /// <param name="after">变更后的集合</param>
    /// <param name="labelOf">元素到展示文本的转换；为空时直接使用元素本身</param>
    public static string Diff(
        IEnumerable<string>? before,
        IEnumerable<string>? after,
        Func<string, string>? labelOf = null)
    {
        var beforeSet = ToSet(before);
        var afterSet = ToSet(after);
        Func<string, string> label = labelOf ?? (x => x);

        var added = afterSet.Except(beforeSet, StringComparer.Ordinal).Select(x => $"+{label(x)}");
        var removed = beforeSet.Except(afterSet, StringComparer.Ordinal).Select(x => $"-{label(x)}");
        var parts = added.Concat(removed).ToList();

        return parts.Count == 0 ? Empty : string.Join(" ", parts);
    }

    private static HashSet<string> ToSet(IEnumerable<string>? values)
        => new((values ?? []).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()), StringComparer.Ordinal);
}
