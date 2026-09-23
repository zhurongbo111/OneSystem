using App.Core.Abstractions;
using App.Core.Exports;

namespace App.Core.Features.StockTakes.ExportStockTakes;

/// <summary>
/// 盘点单导出用例（erp-export）：复用列表筛选取全量单据 + 一次批量取明细分组，
/// 输出「单据 + 明细」两个工作表（明细首列为所属单号），明细商品编码 / 名称取快照字段
/// </summary>
public sealed class ExportStockTakesRequestHandler : IRequestHandler<ExportStockTakesRequest, ExportResultDto>
{
    private readonly IStockTakeRepository _stockTakeRepository;
    private readonly IUserRepository _userRepository;
    private readonly IExcelExporter _excelExporter;

    /// <summary>
    /// 初始化盘点单导出用例处理器
    /// </summary>
    public ExportStockTakesRequestHandler(
        IStockTakeRepository stockTakeRepository,
        IUserRepository userRepository,
        IExcelExporter excelExporter)
    {
        _stockTakeRepository = stockTakeRepository;
        _userRepository = userRepository;
        _excelExporter = excelExporter;
    }

    /// <summary>
    /// 处理盘点单导出请求
    /// </summary>
    /// <param name="request">导出请求（筛选参数与列表一致）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ExportResultDto> HandleAsync(ExportStockTakesRequest request, CancellationToken cancellationToken = default)
    {
        // 按上限 + 1 取数：超出上限即明确报错，不静默截断（分页参数不参与，导出当前筛选全量）
        var (takes, _) = await _stockTakeRepository.GetPagedAsync(
            request.Keyword,
            request.Type,
            request.Start,
            request.End,
            request.WarehouseId,
            1,
            ExportFieldConstraints.MaxRows + 1,
            cancellationToken);
        ExportGuards.EnsureWithinRowLimit(takes.Count);

        // 明细一次批量取回（避免逐单查询 N+1），再按单据 id 分组
        var takeIds = takes.Select(t => t.Id).ToList();
        var items = await _stockTakeRepository.GetItemsByTakeIdsAsync(takeIds, cancellationToken);
        ExportGuards.EnsureWithinRowLimit(items.Count);

        var creatorNames = await _userRepository.GetDisplayNamesByIdsAsync(
            takes.Where(t => t.CreatedBy is not null).Select(t => t.CreatedBy!.Value).Distinct().ToList(),
            cancellationToken);

        var itemsByTake = items.ToLookup(i => i.StockTakeId);

        var takeRows = new List<IReadOnlyList<object?>>(takes.Count);
        var itemRows = new List<IReadOnlyList<object?>>(items.Count);
        foreach (var take in takes)
        {
            takeRows.Add(new object?[]
            {
                take.TakeNo,
                ExportLabels.ToText(take.Type),
                take.WarehouseName,
                take.TakeDate,
                take.ItemCount,
                take.DiffItemCount,
                take.Remark ?? string.Empty,
                take.CreatedAt,
                take.CreatedBy is not null && creatorNames.TryGetValue(take.CreatedBy.Value, out var creator) ? creator : string.Empty,
            });

            foreach (var item in itemsByTake[take.Id])
            {
                itemRows.Add(new object?[]
                {
                    take.TakeNo,
                    item.ProductCode,
                    item.ProductName,
                    item.Unit,
                    item.BookQuantity,
                    item.ActualQuantity,
                    item.Difference,
                    item.UnitCost,
                });
            }
        }

        var workbook = new ExcelWorkbookModel
        {
            Sheets =
            [
                new ExcelSheetModel
                {
                    Name = ExportDomainNames.DocumentSheet,
                    Columns =
                    [
                        new ExcelColumnModel { Header = "单号", ValueType = ExcelValueType.Text, Width = 20 },
                        new ExcelColumnModel { Header = "类型", ValueType = ExcelValueType.Text, Width = 12 },
                        new ExcelColumnModel { Header = "仓库", ValueType = ExcelValueType.Text, Width = 16 },
                        new ExcelColumnModel { Header = "盘点日期", ValueType = ExcelValueType.Date, Width = 14 },
                        new ExcelColumnModel { Header = "明细行数", ValueType = ExcelValueType.Integer, Width = 12 },
                        new ExcelColumnModel { Header = "差异行数", ValueType = ExcelValueType.Integer, Width = 12 },
                        new ExcelColumnModel { Header = "备注", ValueType = ExcelValueType.Text, Width = 28 },
                        new ExcelColumnModel { Header = "创建时间", ValueType = ExcelValueType.DateTime, Width = 18 },
                        new ExcelColumnModel { Header = "创建人", ValueType = ExcelValueType.Text, Width = 14 },
                    ],
                    Rows = takeRows,
                },
                new ExcelSheetModel
                {
                    Name = ExportDomainNames.DetailSheet,
                    Columns =
                    [
                        new ExcelColumnModel { Header = "单号", ValueType = ExcelValueType.Text, Width = 20 },
                        new ExcelColumnModel { Header = "商品编码", ValueType = ExcelValueType.Text, Width = 18 },
                        new ExcelColumnModel { Header = "商品名称", ValueType = ExcelValueType.Text, Width = 24 },
                        new ExcelColumnModel { Header = "单位", ValueType = ExcelValueType.Text, Width = 8 },
                        new ExcelColumnModel { Header = "账面数量", ValueType = ExcelValueType.Integer, Width = 12 },
                        new ExcelColumnModel { Header = "实盘数量", ValueType = ExcelValueType.Integer, Width = 12 },
                        new ExcelColumnModel { Header = "差异", ValueType = ExcelValueType.Integer, Width = 10 },
                        new ExcelColumnModel { Header = "成本单价", ValueType = ExcelValueType.Decimal, Width = 12 },
                    ],
                    Rows = itemRows,
                },
            ],
        };

        return new ExportResultDto
        {
            FileName = ExportFileNames.Build(ExportDomainNames.StockTakes, DateTimeOffset.UtcNow),
            Content = _excelExporter.Build(workbook),
        };
    }
}
