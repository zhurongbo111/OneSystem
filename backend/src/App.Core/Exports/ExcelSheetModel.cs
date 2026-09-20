namespace App.Core.Exports;

/// <summary>
/// Excel 工作表模型：列定义 + 数据行 + 可选合计行（合计行加粗并带上边框）。
/// 单元格值与 <see cref="ExcelColumnModel.ValueType"/> 一一对应，由导出实现负责类型转换。
/// </summary>
public sealed record ExcelSheetModel
{
    /// <summary>工作表名称（超 31 字符或含非法字符时由导出实现截断 / 净化）</summary>
    public required string Name { get; init; }

    /// <summary>列定义（顺序即导出列顺序）</summary>
    public required IReadOnlyList<ExcelColumnModel> Columns { get; init; }

    /// <summary>数据行；每行长度可小于列数（缺失单元格按空值输出）</summary>
    public required IReadOnlyList<IReadOnlyList<object?>> Rows { get; init; }

    /// <summary>合计行，可空；为 null 时不输出合计行</summary>
    public IReadOnlyList<object?>? TotalRow { get; init; }
}
