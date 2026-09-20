using App.Core.Abstractions;
using App.Core.Exports;

namespace App.Core.Features.Reports.ExportSalesSummary;

/// <summary>
/// 销售汇总导出用例（erp-export）：复用报表聚合查询取全量，导出列 = 报表列定义（去序号 / 操作列），
/// 末行输出与页面口径一致的合计行（出库单数 / 出库数量与金额 / 退货数量与金额 / 净数量与净额）
/// </summary>
public sealed class ExportSalesSummaryRequestHandler : IRequestHandler<ExportSalesSummaryRequest, ExportResultDto>
{
    private readonly IReportQueryRepository _reportQueryRepository;
    private readonly IExcelExporter _excelExporter;

    /// <summary>
    /// 初始化销售汇总导出用例处理器
    /// </summary>
    /// <param name="reportQueryRepository">报表只读查询仓储</param>
    /// <param name="excelExporter">Excel 导出器</param>
    public ExportSalesSummaryRequestHandler(
        IReportQueryRepository reportQueryRepository,
        IExcelExporter excelExporter)
    {
        _reportQueryRepository = reportQueryRepository;
        _excelExporter = excelExporter;
    }

    /// <summary>
    /// 处理销售汇总导出请求
    /// </summary>
    /// <param name="request">导出请求（筛选参数与报表一致）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ExportResultDto> HandleAsync(
        ExportSalesSummaryRequest request, CancellationToken cancellationToken = default)
    {
        // 期间必填由 Validator 保证（全局校验在分发前执行）
        var groupByProduct = request.GroupBy == SummaryGroupBy.Product;
        var (items, _, summary) = await _reportQueryRepository.GetSalesSummaryAsync(
            request.Start!.Value,
            request.End!.Value,
            request.PartnerId,
            groupByProduct,
            1,
            ExportFieldConstraints.MaxRows + 1,
            cancellationToken);
        ExportGuards.EnsureWithinRowLimit(items.Count);

        // 净数量 / 净金额与页面同口径（复用正向映射，避免公式分叉）
        // 单位列：商品维度为商品单位；客户维度聚合的是多种商品、无单一单位 → 输出占位符（不留空白单元格）
        var rows = items
            .Select(item => ReportsDtoMapper.ToSalesSummaryItemDto(item))
            .Select(dto => (IReadOnlyList<object?>)new object?[]
            {
                dto.Name,
                ExportLabels.OrDash(dto.Unit),
                dto.OrderCount,
                dto.OutboundQuantity,
                dto.OutboundAmount,
                dto.ReturnQuantity,
                dto.ReturnAmount,
                dto.NetQuantity,
                dto.NetAmount,
            })
            .ToList();

        var total = ReportsDtoMapper.ToSalesSummaryTotalDto(summary);

        var sheet = new ExcelSheetModel
        {
            Name = ExportDomainNames.SalesSummary,
            Columns =
            [
                new ExcelColumnModel
                {
                    Header = groupByProduct ? "商品" : "客户",
                    ValueType = ExcelValueType.Text,
                    Width = 20,
                },
                new ExcelColumnModel { Header = "单位", ValueType = ExcelValueType.Text, Width = 8 },
                new ExcelColumnModel { Header = "出库单数", ValueType = ExcelValueType.Integer, Width = 10 },
                new ExcelColumnModel { Header = "出库数量", ValueType = ExcelValueType.Integer, Width = 12 },
                new ExcelColumnModel { Header = "出库金额", ValueType = ExcelValueType.Decimal, Width = 14 },
                new ExcelColumnModel { Header = "退货数量", ValueType = ExcelValueType.Integer, Width = 12 },
                new ExcelColumnModel { Header = "退货金额", ValueType = ExcelValueType.Decimal, Width = 14 },
                new ExcelColumnModel { Header = "净数量", ValueType = ExcelValueType.Integer, Width = 12 },
                new ExcelColumnModel { Header = "净金额", ValueType = ExcelValueType.Decimal, Width = 14 },
            ],
            Rows = rows,
            TotalRow =
            [
                "合计",
                "-",
                total.OrderCount,
                total.OutboundQuantity,
                total.OutboundAmount,
                total.ReturnQuantity,
                total.ReturnAmount,
                total.NetQuantity,
                total.NetAmount,
            ],
        };

        var workbook = new ExcelWorkbookModel { Sheets = [sheet] };

        return new ExportResultDto
        {
            FileName = ExportFileNames.Build(ExportDomainNames.SalesSummary, DateTimeOffset.UtcNow),
            Content = _excelExporter.Build(workbook),
        };
    }
}
