using App.Core.Abstractions;
using App.Core.Exports;

namespace App.Core.Features.Reports.ExportCostProfit;

/// <summary>
/// 成本与毛利报表导出用例（erp-export）：复用报表聚合查询取全量，导出列 = 报表列定义（去序号 / 操作列），
/// 末行输出与页面口径一致的合计行（销售数量 / 销售收入 / 销售成本 / 毛利 / 毛利率 / 成本完整性）
/// </summary>
public sealed class ExportCostProfitRequestHandler : IRequestHandler<ExportCostProfitRequest, ExportResultDto>
{
    private readonly IReportQueryRepository _reportQueryRepository;
    private readonly IExcelExporter _excelExporter;

    /// <summary>
    /// 初始化成本与毛利报表导出用例处理器
    /// </summary>
    /// <param name="reportQueryRepository">报表只读查询仓储</param>
    /// <param name="excelExporter">Excel 导出器</param>
    public ExportCostProfitRequestHandler(
        IReportQueryRepository reportQueryRepository,
        IExcelExporter excelExporter)
    {
        _reportQueryRepository = reportQueryRepository;
        _excelExporter = excelExporter;
    }

    /// <summary>
    /// 处理成本与毛利报表导出请求
    /// </summary>
    /// <param name="request">导出请求（筛选参数与报表一致）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ExportResultDto> HandleAsync(
        ExportCostProfitRequest request, CancellationToken cancellationToken = default)
    {
        // 期间必填由 Validator 保证（全局校验在分发前执行）
        var (items, _, summary) = await _reportQueryRepository.GetCostProfitAsync(
            request.Start!.Value,
            request.End!.Value,
            request.ProductId,
            request.CategoryId,
            request.GroupBy,
            1,
            ExportFieldConstraints.MaxRows + 1,
            cancellationToken);
        ExportGuards.EnsureWithinRowLimit(items.Count);

        // 毛利 / 毛利率与页面同口径（复用正向映射，收入为 0 时毛利率为 null）
        var rows = items
            .Select(item => ReportsDtoMapper.ToCostProfitItemDto(item))
            .Select(dto => (IReadOnlyList<object?>)new object?[]
            {
                dto.Name,
                dto.SalesQuantity,
                dto.SalesAmount,
                dto.CostAmount,
                dto.GrossProfit,
                FormatRate(dto.GrossProfitRate),
                FormatCostCompleteness(dto.HasMissingCost),
            })
            .ToList();

        var total = ReportsDtoMapper.ToCostProfitTotalDto(summary);

        var sheet = new ExcelSheetModel
        {
            Name = ExportDomainNames.CostProfit,
            Columns =
            [
                new ExcelColumnModel
                {
                    Header = ResolveNameHeader(request.GroupBy),
                    ValueType = ExcelValueType.Text,
                    Width = 20,
                },
                new ExcelColumnModel { Header = "销售数量", ValueType = ExcelValueType.Integer, Width = 12 },
                new ExcelColumnModel { Header = "销售收入", ValueType = ExcelValueType.Decimal, Width = 14 },
                new ExcelColumnModel { Header = "销售成本", ValueType = ExcelValueType.Decimal, Width = 14 },
                new ExcelColumnModel { Header = "毛利", ValueType = ExcelValueType.Decimal, Width = 14 },
                new ExcelColumnModel { Header = "毛利率", ValueType = ExcelValueType.Text, Width = 12 },
                new ExcelColumnModel { Header = "成本完整性", ValueType = ExcelValueType.Text, Width = 14 },
            ],
            Rows = rows,
            TotalRow =
            [
                "合计",
                total.SalesQuantity,
                total.SalesAmount,
                total.CostAmount,
                total.GrossProfit,
                FormatRate(total.GrossProfitRate),
                FormatCostCompleteness(total.HasMissingCost),
            ],
        };

        var workbook = new ExcelWorkbookModel { Sheets = [sheet] };

        return new ExportResultDto
        {
            FileName = ExportFileNames.Build(ExportDomainNames.CostProfit, DateTimeOffset.UtcNow),
            Content = _excelExporter.Build(workbook),
        };
    }

    /// <summary>首列表头按分组维度取页面列标题（单据号 / 商品 / 往来单位）</summary>
    private static string ResolveNameHeader(CostProfitGroupBy groupBy)
        => groupBy switch
        {
            CostProfitGroupBy.Product => "商品",
            CostProfitGroupBy.Partner => "往来单位",
            _ => "单据号",
        };

    /// <summary>毛利率输出为百分比文本（0.12 → <c>12.00%</c>）；收入为 0 时为 null（空单元格）</summary>
    private static string? FormatRate(decimal? rate) => rate is null ? null : $"{rate.Value * 100:0.00}%";

    /// <summary>成本完整性按页面列文案输出（缺失为「成本不完整」，否则「-」）</summary>
    private static string FormatCostCompleteness(bool hasMissingCost) => hasMissingCost ? "成本不完整" : "-";
}
