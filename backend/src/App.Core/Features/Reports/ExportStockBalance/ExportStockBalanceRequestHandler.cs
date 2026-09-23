using App.Core.Abstractions;
using App.Core.Exports;

namespace App.Core.Features.Reports.ExportStockBalance;

/// <summary>
/// 库存余额表导出用例（erp-export）：复用报表聚合查询取全量，导出列 = 报表列定义（去序号 / 操作列），
/// 末行输出与页面口径一致的合计行（商品数 / 库存合计 / 零库存商品数 / 低库存商品数 / 库存金额）
/// </summary>
public sealed class ExportStockBalanceRequestHandler : IRequestHandler<ExportStockBalanceRequest, ExportResultDto>
{
    private readonly IReportQueryRepository _reportQueryRepository;
    private readonly IExcelExporter _excelExporter;

    /// <summary>
    /// 初始化库存余额表导出用例处理器
    /// </summary>
    /// <param name="reportQueryRepository">报表只读查询仓储</param>
    /// <param name="excelExporter">Excel 导出器</param>
    public ExportStockBalanceRequestHandler(
        IReportQueryRepository reportQueryRepository,
        IExcelExporter excelExporter)
    {
        _reportQueryRepository = reportQueryRepository;
        _excelExporter = excelExporter;
    }

    /// <summary>
    /// 处理库存余额表导出请求
    /// </summary>
    /// <param name="request">导出请求（筛选参数与报表一致）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ExportResultDto> HandleAsync(
        ExportStockBalanceRequest request, CancellationToken cancellationToken = default)
    {
        var (items, _, summary) = await _reportQueryRepository.GetStockBalanceAsync(
            request.Keyword,
            request.CategoryId,
            request.WarehouseId,
            1,
            ExportFieldConstraints.MaxRows + 1,
            cancellationToken);
        ExportGuards.EnsureWithinRowLimit(items.Count);

        // 占比分母为全量筛选库存总量、均价口径与页面一致：复用正向映射取派生值，避免公式分叉
        var rows = items
            .Select(item => ReportsDtoMapper.ToStockBalanceItemDto(item, summary.TotalQuantity))
            .Select(dto => (IReadOnlyList<object?>)new object?[]
            {
                dto.CategoryName,
                dto.ProductCount,
                dto.TotalQuantity,
                dto.ZeroStockCount,
                dto.BelowSafetyCount,
                FormatRatio(dto.QuantityRatio),
                dto.TotalCostAmount,
                dto.AverageCost,
                FormatCostAnomaly(dto.HasCostAnomaly),
            })
            .ToList();

        var sheet = new ExcelSheetModel
        {
            Name = ExportDomainNames.StockBalance,
            Columns =
            [
                new ExcelColumnModel { Header = "分类", ValueType = ExcelValueType.Text, Width = 20 },
                new ExcelColumnModel { Header = "商品数", ValueType = ExcelValueType.Integer, Width = 10 },
                new ExcelColumnModel { Header = "库存合计", ValueType = ExcelValueType.Integer, Width = 12 },
                new ExcelColumnModel { Header = "零库存商品数", ValueType = ExcelValueType.Integer, Width = 14 },
                new ExcelColumnModel { Header = "低库存商品数", ValueType = ExcelValueType.Integer, Width = 14 },
                new ExcelColumnModel { Header = "库存占比", ValueType = ExcelValueType.Text, Width = 12 },
                new ExcelColumnModel { Header = "库存金额", ValueType = ExcelValueType.Decimal, Width = 14 },
                new ExcelColumnModel { Header = "均价", ValueType = ExcelValueType.Decimal, Width = 12 },
                new ExcelColumnModel { Header = "成本异常", ValueType = ExcelValueType.Text, Width = 12 },
            ],
            Rows = rows,
            TotalRow =
            [
                "合计",
                summary.ProductCount,
                summary.TotalQuantity,
                summary.ZeroStockCount,
                summary.BelowSafetyCount,
                null,
                summary.TotalCostAmount,
                null,
                null,
            ],
        };

        var workbook = new ExcelWorkbookModel { Sheets = [sheet] };

        return new ExportResultDto
        {
            FileName = ExportFileNames.Build(ExportDomainNames.StockBalance, DateTimeOffset.UtcNow),
            Content = _excelExporter.Build(workbook),
        };
    }

    /// <summary>库存占比输出为百分比文本（0.25 → <c>25.00%</c>）</summary>
    private static string FormatRatio(decimal ratio) => $"{ratio * 100:0.00}%";

    /// <summary>成本异常标记按页面列文案输出（异常为「成本异常」，否则「-」）</summary>
    private static string FormatCostAnomaly(bool hasCostAnomaly) => hasCostAnomaly ? "成本异常" : "-";
}
