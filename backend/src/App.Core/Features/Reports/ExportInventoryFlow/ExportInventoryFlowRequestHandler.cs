using App.Core.Abstractions;
using App.Core.Exports;

namespace App.Core.Features.Reports.ExportInventoryFlow;

/// <summary>
/// 进销存报表导出用例（erp-export）：复用报表聚合查询取全量，导出列 = 报表列定义，
/// 末行输出与页面口径一致的合计行（期初 / 期间入 / 期间出 / 期末）
/// </summary>
public sealed class ExportInventoryFlowRequestHandler : IRequestHandler<ExportInventoryFlowRequest, ExportResultDto>
{
    private readonly IReportQueryRepository _reportQueryRepository;
    private readonly IExcelExporter _excelExporter;

    /// <summary>
    /// 初始化进销存报表导出用例处理器
    /// </summary>
    public ExportInventoryFlowRequestHandler(IReportQueryRepository reportQueryRepository, IExcelExporter excelExporter)
    {
        _reportQueryRepository = reportQueryRepository;
        _excelExporter = excelExporter;
    }

    /// <summary>
    /// 处理进销存报表导出请求
    /// </summary>
    /// <param name="request">导出请求（筛选参数与报表一致）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ExportResultDto> HandleAsync(ExportInventoryFlowRequest request, CancellationToken cancellationToken = default)
    {
        // 期间必填由 Validator 保证（全局校验在分发前执行）
        var (items, _, summary) = await _reportQueryRepository.GetInventoryFlowAsync(
            request.Start!.Value,
            request.End!.Value,
            request.ProductId,
            request.CategoryId,
            request.OnlyChanged,
            request.WarehouseId,
            1,
            ExportFieldConstraints.MaxRows + 1,
            cancellationToken);
        ExportGuards.EnsureWithinRowLimit(items.Count);

        var rows = items
            .Select(item => (IReadOnlyList<object?>)new object?[]
            {
                item.Code,
                item.Name,
                item.CategoryName,
                item.Unit,
                item.OpeningQuantity,
                item.InboundQuantity,
                item.OutboundQuantity,
                item.ClosingQuantity,
            })
            .ToList();

        var sheet = new ExcelSheetModel
        {
            Name = ExportDomainNames.InventoryFlow,
            Columns =
            [
                new ExcelColumnModel { Header = "商品编码", ValueType = ExcelValueType.Text, Width = 18 },
                new ExcelColumnModel { Header = "商品名称", ValueType = ExcelValueType.Text, Width = 24 },
                new ExcelColumnModel { Header = "分类", ValueType = ExcelValueType.Text, Width = 14 },
                new ExcelColumnModel { Header = "单位", ValueType = ExcelValueType.Text, Width = 8 },
                new ExcelColumnModel { Header = "期初数量", ValueType = ExcelValueType.Integer, Width = 12 },
                new ExcelColumnModel { Header = "期间入", ValueType = ExcelValueType.Integer, Width = 12 },
                new ExcelColumnModel { Header = "期间出", ValueType = ExcelValueType.Integer, Width = 12 },
                new ExcelColumnModel { Header = "期末数量", ValueType = ExcelValueType.Integer, Width = 12 },
            ],
            Rows = rows,
            TotalRow =
            [
                "合计",
                null,
                null,
                null,
                summary.OpeningQuantity,
                summary.InboundQuantity,
                summary.OutboundQuantity,
                summary.ClosingQuantity,
            ],
        };

        var workbook = new ExcelWorkbookModel { Sheets = [sheet] };

        return new ExportResultDto
        {
            FileName = ExportFileNames.Build(ExportDomainNames.InventoryFlow, DateTimeOffset.UtcNow),
            Content = _excelExporter.Build(workbook),
        };
    }
}
