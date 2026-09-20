using App.Core.Exports;

namespace App.Core.Abstractions;

/// <summary>
/// Excel 导出器：只认识「表格模型」（工作表 / 列 / 行 / 合计行），不认识任何业务类型。
/// 各域导出用例负责把 DTO 映射为表格模型，实现（ClosedXML）在 App.Infrastructure。
/// </summary>
public interface IExcelExporter
{
    /// <summary>
    /// 按表格模型生成 xlsx 二进制内容（同步纯计算，数据量受 ExportFieldConstraints.MaxRows 约束）。
    /// </summary>
    /// <param name="model">工作簿模型（至少一个工作表）</param>
    /// <returns>xlsx 文件内容</returns>
    byte[] Build(ExcelWorkbookModel model);
}
