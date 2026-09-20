using System.Collections;

using App.Core.Abstractions;
using App.Core.Errors;
using App.Core.Exports;
using App.Infrastructure.Exports;

using ClosedXML.Excel;

namespace App.Tests;

/// <summary>
/// ClosedXmlExcelExporter 测试：round-trip 可重新打开、多工作表 + 合计行、格式化（金额 / 整数 / 日期）、
/// 工作表名截断与净化、空数据仅表头、Excel 单表行数上限。
/// </summary>
public class ClosedXmlExcelExporterTests
{
    private static readonly IExcelExporter Exporter = new ClosedXmlExcelExporter();

    private static readonly DateTimeOffset BaseTime = new(2026, 9, 17, 10, 30, 0, TimeSpan.Zero);

    private static ExcelWorkbookModel SingleSheet(string name, ExcelSheetModel? sheet = null, int rows = 1)
        => new() { Sheets = [sheet ?? NewSheet(name, rows)] };

    private static ExcelSheetModel NewSheet(string name, int rows = 1, IReadOnlyList<object?>? totalRow = null)
        => new()
        {
            Name = name,
            Columns =
            [
                new ExcelColumnModel { Header = "单号", ValueType = ExcelValueType.Text },
                new ExcelColumnModel { Header = "金额", ValueType = ExcelValueType.Decimal },
                new ExcelColumnModel { Header = "数量", ValueType = ExcelValueType.Integer },
                new ExcelColumnModel { Header = "日期", ValueType = ExcelValueType.Date },
                new ExcelColumnModel { Header = "时间", ValueType = ExcelValueType.DateTime },
            ],
            Rows = Enumerable.Range(1, rows)
                .Select(i => (IReadOnlyList<object?>)new object?[]
                {
                    $"GR20260917000{i}",
                    100m * i + 0.125m,
                    i,
                    BaseTime.UtcDateTime.Date,
                    BaseTime.UtcDateTime,
                })
                .ToList(),
            TotalRow = totalRow,
        };

    [Fact]
    public void 生成的内容_可被重新打开且表头与数据一致()
    {
        var bytes = Exporter.Build(SingleSheet("单据"));

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("单据");

        Assert.Equal("单号", sheet.Cell(1, 1).GetString());
        Assert.Equal("金额", sheet.Cell(1, 2).GetString());
        Assert.True(sheet.Cell(1, 1).Style.Font.Bold);
        Assert.Equal("GR202609170001", sheet.Cell(2, 1).GetString());
        Assert.Equal(100.125, sheet.Cell(2, 2).GetDouble(), 4);
        Assert.Equal(1, sheet.Cell(2, 3).GetValue<int>());
        Assert.Equal(BaseTime.UtcDateTime.Date, sheet.Cell(2, 4).GetDateTime());
        Assert.Equal(BaseTime.UtcDateTime, sheet.Cell(2, 5).GetDateTime());
    }

    [Fact]
    public void 金额与日期_按约定格式输出()
    {
        var bytes = Exporter.Build(SingleSheet("商品"));

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("商品");

        Assert.Equal("0.00", sheet.Cell(2, 2).Style.NumberFormat.Format);
        Assert.Equal("0", sheet.Cell(2, 3).Style.NumberFormat.Format);
        Assert.Equal("yyyy-mm-dd", sheet.Cell(2, 4).Style.NumberFormat.Format);
        Assert.Equal("yyyy-mm-dd hh:mm", sheet.Cell(2, 5).Style.NumberFormat.Format);
    }

    [Fact]
    public void 多工作表与合计行_合计行加粗且带顶部边框()
    {
        var model = new ExcelWorkbookModel
        {
            Sheets =
            [
                NewSheet("单据"),
                NewSheet("明细", rows: 2, totalRow: ["合计", 300.25m, 3, null, null]),
            ],
        };

        var bytes = Exporter.Build(model);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(2, workbook.Worksheets.Count);
        var detail = workbook.Worksheet("明细");

        // 数据 2 行 + 表头 → 合计行在第 4 行
        Assert.Equal("合计", detail.Cell(4, 1).GetString());
        Assert.Equal(300.25, detail.Cell(4, 2).GetDouble(), 4);
        Assert.Equal(3, detail.Cell(4, 3).GetValue<int>());
        Assert.True(detail.Cell(4, 1).Style.Font.Bold);
        Assert.Equal(XLBorderStyleValues.Thin, detail.Cell(4, 1).Style.Border.TopBorder);
    }

    [Fact]
    public void 空数据_仅输出表头()
    {
        var bytes = Exporter.Build(SingleSheet("空表", NewSheet("空表", rows: 0)));

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("空表");

        Assert.Equal("单号", sheet.Cell(1, 1).GetString());
        Assert.Equal(1, sheet.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public void 工作表名_超长截断且净化非法字符()
    {
        var longName = new string('长', 40) + ":单据/明细";
        var model = new ExcelWorkbookModel
        {
            Sheets = [NewSheet(longName, rows: 0), NewSheet(longName, rows: 0)],
        };

        var bytes = Exporter.Build(model);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var names = workbook.Worksheets.Select(w => w.Name).ToList();

        Assert.Equal(2, names.Count);
        Assert.All(names, name => Assert.True(name.Length <= 31, $"工作表名超长：{name}"));
        Assert.All(names, name => Assert.DoesNotContain(':', name));
        Assert.All(names, name => Assert.DoesNotContain('/', name));
        // 重名时追加序号，保证两个工作表都可访问
        Assert.NotEqual(names[0], names[1]);
    }

    [Fact]
    public void 超过Excel单表上限_抛参数错误()
    {
        var model = new ExcelWorkbookModel
        {
            Sheets =
            [
                new ExcelSheetModel
                {
                    Name = "超限",
                    Columns = [new ExcelColumnModel { Header = "单号", ValueType = ExcelValueType.Text }],
                    // 仅用于触发上限判定，不实际写入 100 万行
                    Rows = new CountingRowList(1_048_577),
                },
            ],
        };

        var exception = Assert.Throws<BusinessException>(() => Exporter.Build(model));
        Assert.Equal(ErrorCode.Validation, exception.Code);
    }

    [Fact]
    public void 无工作表_抛参数错误()
    {
        var exception = Assert.Throws<BusinessException>(() => Exporter.Build(new ExcelWorkbookModel { Sheets = [] }));
        Assert.Equal(ErrorCode.Validation, exception.Code);
    }

    [Fact]
    public void 导出上限判定_边界值通过_超出拒绝()
    {
        ExportGuards.EnsureWithinRowLimit(ExportFieldConstraints.MaxRows);

        var exception = Assert.Throws<BusinessException>(
            () => ExportGuards.EnsureWithinRowLimit(ExportFieldConstraints.MaxRows + 1));
        Assert.Equal(ErrorCode.Validation, exception.Code);
        Assert.Contains("50000", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>只声明行数、不实际分配行对象的只读列表（用于验证 Excel 单表行数上限判定）</summary>
    private sealed class CountingRowList : IReadOnlyList<IReadOnlyList<object?>>
    {
        private static readonly IReadOnlyList<object?> EmptyRow = [];

        public CountingRowList(int count) => Count = count;

        public int Count { get; }

        public IReadOnlyList<object?> this[int index] => EmptyRow;

        public IEnumerator<IReadOnlyList<object?>> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
            {
                yield return EmptyRow;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
