namespace App.Core.Exports;

/// <summary>
/// Excel 工作簿模型：一次导出包含的一个或多个工作表（单据类导出为「单据 + 明细」两张）。
/// </summary>
public sealed record ExcelWorkbookModel
{
    /// <summary>工作表集合（顺序即工作表顺序，至少一张）</summary>
    public required IReadOnlyList<ExcelSheetModel> Sheets { get; init; }
}
