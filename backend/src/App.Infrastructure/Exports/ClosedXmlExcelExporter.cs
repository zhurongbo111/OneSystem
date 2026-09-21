using System.Globalization;

using App.Core.Abstractions;
using App.Core.Errors;
using App.Core.Exports;

using ClosedXML.Excel;

namespace App.Infrastructure.Exports;

/// <summary>
/// <see cref="IExcelExporter"/> 的 ClosedXML 实现（无状态，注册为 Singleton）。
/// 只认识表格模型：首行表头加粗并冻结、列宽按给定值或内容估算、金额 2 位小数、日期 / 日期时间格式化、
/// 合计行加粗并带顶部细边框；工作表名超 31 字符截断并净化非法字符。
/// </summary>
public sealed class ClosedXmlExcelExporter : IExcelExporter
{
    /// <summary>Excel 单个工作表的行数上限（含表头与合计行）</summary>
    private const int MaxWorksheetRows = 1048576;

    /// <summary>工作表名最大长度</summary>
    private const int MaxSheetNameLength = 31;

    /// <summary>列宽估算下限 / 上限（字符数）</summary>
    private const double MinColumnWidth = 8;
    private const double MaxColumnWidth = 40;

    /// <summary>工作表名中不允许出现的字符（Excel 限制）</summary>
    private static readonly char[] _invalidSheetNameChars = [':', '\\', '/', '?', '*', '[', ']'];

    /// <inheritdoc />
    public byte[] Build(ExcelWorkbookModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.Sheets.Count == 0)
        {
            throw new BusinessException(ErrorCode.Validation, "导出内容为空，至少需要一个工作表");
        }

        using var workbook = new XLWorkbook();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sheet in model.Sheets)
        {
            AddSheet(workbook, sheet, usedNames);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// 写入单个工作表（表头 / 数据行 / 合计行 / 列宽 / 冻结首行）。
    /// </summary>
    /// <param name="workbook">目标工作簿</param>
    /// <param name="sheet">工作表模型</param>
    /// <param name="usedNames">已占用工作表名（避免重名导致导出失败）</param>
    private static void AddSheet(XLWorkbook workbook, ExcelSheetModel sheet, HashSet<string> usedNames)
    {
        var rowCount = 1 + sheet.Rows.Count + (sheet.TotalRow is null ? 0 : 1);
        if (rowCount > MaxWorksheetRows)
        {
            throw new BusinessException(
                ErrorCode.Validation,
                $"导出数据超过 Excel 单表上限 {MaxWorksheetRows} 行，请缩小筛选范围后重试");
        }

        var worksheet = workbook.Worksheets.Add(BuildSheetName(sheet.Name, usedNames));

        // 表头：加粗 + 冻结首行
        for (var index = 0; index < sheet.Columns.Count; index++)
        {
            var cell = worksheet.Cell(1, index + 1);
            cell.Value = sheet.Columns[index].Header;
            cell.Style.Font.Bold = true;
        }

        worksheet.SheetView.FreezeRows(1);

        // 数据行（从第 2 行开始）
        for (var rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++)
        {
            var values = sheet.Rows[rowIndex];
            for (var columnIndex = 0; columnIndex < sheet.Columns.Count; columnIndex++)
            {
                var value = columnIndex < values.Count ? values[columnIndex] : null;
                SetCellValue(worksheet.Cell(rowIndex + 2, columnIndex + 1), value, sheet.Columns[columnIndex].ValueType);
            }
        }

        // 合计行：加粗 + 顶部细边框（紧跟数据行）
        if (sheet.TotalRow is not null)
        {
            var totalRowNumber = sheet.Rows.Count + 2;
            for (var columnIndex = 0; columnIndex < sheet.Columns.Count; columnIndex++)
            {
                var value = columnIndex < sheet.TotalRow.Count ? sheet.TotalRow[columnIndex] : null;
                var cell = worksheet.Cell(totalRowNumber, columnIndex + 1);
                SetCellValue(cell, value, sheet.Columns[columnIndex].ValueType);
                cell.Style.Font.Bold = true;
                cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            }
        }

        // 列宽：先按内容估算，再覆盖显式给定的列宽
        worksheet.Columns().AdjustToContents(MinColumnWidth, MaxColumnWidth);
        for (var index = 0; index < sheet.Columns.Count; index++)
        {
            var width = sheet.Columns[index].Width;
            if (width is not null)
            {
                worksheet.Column(index + 1).Width = width.Value;
            }
        }
    }

    /// <summary>
    /// 按列声明类型写入单元格值（空值写空单元格）。
    /// </summary>
    /// <param name="cell">目标单元格</param>
    /// <param name="value">原始值（可为 null）</param>
    /// <param name="valueType">列值类型</param>
    private static void SetCellValue(IXLCell cell, object? value, ExcelValueType valueType)
    {
        if (value is null)
        {
            return;
        }

        switch (valueType)
        {
            case ExcelValueType.Integer:
                cell.Value = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                cell.Style.NumberFormat.Format = "0";
                break;

            case ExcelValueType.Decimal:
                cell.Value = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                cell.Style.NumberFormat.Format = "0.00";
                break;

            case ExcelValueType.Date:
                cell.Value = ToDateTime(value);
                cell.Style.NumberFormat.Format = "yyyy-mm-dd";
                break;

            case ExcelValueType.DateTime:
                cell.Value = ToDateTime(value);
                cell.Style.NumberFormat.Format = "yyyy-mm-dd hh:mm";
                break;

            default:
                cell.Value = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                break;
        }
    }

    /// <summary>
    /// 归一化时间值：按服务器本地时区输出，与前端 <c>formatDateTime</c>（浏览器本地时区）的展示口径一致。
    /// </summary>
    /// <param name="value">时间值（DateTimeOffset / DateTime / DateOnly / 可解析字符串）</param>
    /// <returns>本地 DateTime</returns>
    private static DateTime ToDateTime(object value)
        => value switch
        {
            DateTimeOffset offset => offset.LocalDateTime,
            DateTime dateTime => dateTime,
            DateOnly dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),
            string text when DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) => parsed.LocalDateTime,
            _ => throw new BusinessException(ErrorCode.Validation, "导出数据中的时间值格式非法"),
        };

    /// <summary>
    /// 生成合法的唯一工作表名：净化 Excel 非法字符、超长截断、重名追加序号。
    /// </summary>
    /// <param name="name">原始名称</param>
    /// <param name="usedNames">已占用名称集合（方法内会登记新名称）</param>
    /// <returns>合法且唯一的工作表名</returns>
    private static string BuildSheetName(string name, HashSet<string> usedNames)
    {
        var sanitized = (name ?? string.Empty).Trim();
        foreach (var invalid in _invalidSheetNameChars)
        {
            sanitized = sanitized.Replace(invalid.ToString(), string.Empty, StringComparison.Ordinal);
        }

        if (sanitized.Length == 0)
        {
            sanitized = "Sheet";
        }

        if (sanitized.Length > MaxSheetNameLength)
        {
            sanitized = sanitized[..MaxSheetNameLength];
        }

        var candidate = sanitized;
        var suffix = 1;
        while (!usedNames.Add(candidate))
        {
            suffix++;
            var tail = $"({suffix})";
            var keep = Math.Min(sanitized.Length, MaxSheetNameLength - tail.Length);
            candidate = sanitized[..keep] + tail;
        }

        return candidate;
    }
}
