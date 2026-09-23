using App.Core.Abstractions;
using App.Core.Exports;

namespace App.Core.Features.StockMovements.ExportStockMovements;

/// <summary>
/// 库存流水导出用例（erp-export）：复用库存流水列表的筛选与仓储查询取全量，
/// 导出列 = 列表列定义（去序号 / 操作列）；操作人即创建人（读模型已带 CreatedByName），不另加创建人列，输出单工作表 xlsx
/// </summary>
public sealed class ExportStockMovementsRequestHandler : IRequestHandler<ExportStockMovementsRequest, ExportResultDto>
{
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IExcelExporter _excelExporter;

    /// <summary>
    /// 初始化库存流水导出用例处理器
    /// </summary>
    public ExportStockMovementsRequestHandler(
        IStockMovementRepository stockMovementRepository,
        IExcelExporter excelExporter)
    {
        _stockMovementRepository = stockMovementRepository;
        _excelExporter = excelExporter;
    }

    /// <summary>
    /// 处理库存流水导出请求
    /// </summary>
    /// <param name="request">导出请求（筛选参数与列表一致）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ExportResultDto> HandleAsync(ExportStockMovementsRequest request, CancellationToken cancellationToken = default)
    {
        // 按上限 + 1 取数：超出上限即明确报错，不静默截断（分页参数不参与，导出当前筛选全量）
        var (items, _) = await _stockMovementRepository.GetPagedAsync(
            request.Keyword,
            request.ProductId,
            request.WarehouseId,
            request.Type,
            request.Start,
            request.End,
            1,
            ExportFieldConstraints.MaxRows + 1,
            cancellationToken);
        ExportGuards.EnsureWithinRowLimit(items.Count);

        var rows = new List<IReadOnlyList<object?>>(items.Count);
        foreach (var item in items)
        {
            rows.Add(new object?[]
            {
                item.CreatedAt,
                item.ProductCode,
                item.ProductName,
                item.Unit,
                item.WarehouseName,
                ExportLabels.ToText(item.MovementType),
                item.Quantity,
                item.UnitCost,
                item.TotalCost,
                item.SourceNo,
                item.CreatedByName,
                item.Remark,
            });
        }

        var sheet = new ExcelSheetModel
        {
            Name = ExportDomainNames.StockMovements,
            Columns =
            [
                new ExcelColumnModel { Header = "变动时间", ValueType = ExcelValueType.DateTime, Width = 18 },
                new ExcelColumnModel { Header = "商品编码", ValueType = ExcelValueType.Text, Width = 18 },
                new ExcelColumnModel { Header = "商品名称", ValueType = ExcelValueType.Text, Width = 24 },
                new ExcelColumnModel { Header = "单位", ValueType = ExcelValueType.Text, Width = 8 },
                new ExcelColumnModel { Header = "仓库", ValueType = ExcelValueType.Text, Width = 16 },
                new ExcelColumnModel { Header = "变动类型", ValueType = ExcelValueType.Text, Width = 12 },
                new ExcelColumnModel { Header = "变动量", ValueType = ExcelValueType.Integer, Width = 10 },
                new ExcelColumnModel { Header = "成本单价", ValueType = ExcelValueType.Decimal, Width = 12 },
                new ExcelColumnModel { Header = "成本金额", ValueType = ExcelValueType.Decimal, Width = 14 },
                new ExcelColumnModel { Header = "来源单号", ValueType = ExcelValueType.Text, Width = 18 },
                new ExcelColumnModel { Header = "操作人", ValueType = ExcelValueType.Text, Width = 14 },
                new ExcelColumnModel { Header = "备注", ValueType = ExcelValueType.Text, Width = 24 },
            ],
            Rows = rows,
        };

        var workbook = new ExcelWorkbookModel { Sheets = [sheet] };

        return new ExportResultDto
        {
            FileName = ExportFileNames.Build(ExportDomainNames.StockMovements, DateTimeOffset.UtcNow),
            Content = _excelExporter.Build(workbook),
        };
    }
}
