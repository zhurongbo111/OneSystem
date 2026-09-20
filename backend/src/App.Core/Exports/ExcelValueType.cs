namespace App.Core.Exports;

/// <summary>
/// Excel 单元格值类型：决定导出时的格式化方式（金额 2 位小数、数量整数、日期 / 日期时间格式）。
/// </summary>
public enum ExcelValueType
{
    /// <summary>文本（原样输出）</summary>
    Text = 0,

    /// <summary>整数（数量类）</summary>
    Integer = 1,

    /// <summary>小数（金额类，保留 2 位小数）</summary>
    Decimal = 2,

    /// <summary>日期（YYYY-MM-DD）</summary>
    Date = 3,

    /// <summary>日期时间（YYYY-MM-DD HH:mm）</summary>
    DateTime = 4,
}
