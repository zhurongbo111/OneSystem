namespace App.Core.Exports;

/// <summary>
/// Excel 列定义（表头 + 值类型 + 可选列宽）；导出表格模型的组成部分，不含业务语义。
/// </summary>
public sealed record ExcelColumnModel
{
    /// <summary>表头文本</summary>
    public required string Header { get; init; }

    /// <summary>值类型（决定单元格格式化方式）</summary>
    public required ExcelValueType ValueType { get; init; }

    /// <summary>列宽（字符数）；不指定时由导出实现按内容估算</summary>
    public double? Width { get; init; }
}
