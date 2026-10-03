using App.Core.Abstractions;
using App.Core.Exports;

namespace App.Core.Features.Inventory.ExportInventory;

/// <summary>
/// 库存查询导出用例（erp-export）：复用库存列表的筛选与仓储查询取全量，
/// 导出列 = 列表列定义（去序号 / 操作列）+ 创建信息，输出单工作表 xlsx
/// </summary>
public sealed class ExportInventoryRequestHandler : IRequestHandler<ExportInventoryRequest, ExportResultDto>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IUserRepository _userRepository;
    private readonly IExcelExporter _excelExporter;

    /// <summary>
    /// 初始化库存查询导出用例处理器
    /// </summary>
    public ExportInventoryRequestHandler(
        IInventoryRepository inventoryRepository,
        IUserRepository userRepository,
        IExcelExporter excelExporter)
    {
        _inventoryRepository = inventoryRepository;
        _userRepository = userRepository;
        _excelExporter = excelExporter;
    }

    /// <summary>
    /// 处理库存查询导出请求
    /// </summary>
    /// <param name="request">导出请求（筛选参数与列表一致）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ExportResultDto> HandleAsync(ExportInventoryRequest request, CancellationToken cancellationToken = default)
    {
        // 按上限 + 1 取数：超出上限即明确报错，不静默截断（分页参数不参与，导出当前筛选全量）
        var (items, _) = await _inventoryRepository.GetPagedAsync(
            request.Keyword,
            request.CategoryId,
            request.WarehouseId,
            request.BatchId,
            request.BatchNo,
            request.ExpandBatch,
            1,
            ExportFieldConstraints.MaxRows + 1,
            cancellationToken);
        ExportGuards.EnsureWithinRowLimit(items.Count);

        var creatorNames = await _userRepository.GetDisplayNamesByIdsAsync(
            items.Where(i => i.CreatedBy is not null).Select(i => i.CreatedBy!.Value).Distinct().ToList(),
            cancellationToken);

        // 展开批次视图：追加「批次号 / 到期日」列、隐藏安全阈值列（040 §4.3）
        var expandBatch = request.ExpandBatch;
        var rows = new List<IReadOnlyList<object?>>(items.Count);
        foreach (var item in items)
        {
            rows.Add(new object?[]
            {
                item.Code,
                item.Name,
                item.CategoryName,
                item.Unit,
                item.WarehouseName,
                expandBatch ? (object?)(item.BatchNo ?? string.Empty) : null,
                expandBatch ? item.ExpiryDate?.ToString("yyyy-MM-dd") : null,
                item.StockQuantity,
                expandBatch ? null : item.SafetyStock,
                item.UpdatedAt,
                item.CreatedAt,
                item.CreatedBy is not null && creatorNames.TryGetValue(item.CreatedBy.Value, out var creator) ? creator : string.Empty,
            });
        }

        var columns = new List<ExcelColumnModel>
        {
            new() { Header = "编码", ValueType = ExcelValueType.Text, Width = 18 },
            new() { Header = "名称", ValueType = ExcelValueType.Text, Width = 24 },
            new() { Header = "分类", ValueType = ExcelValueType.Text, Width = 14 },
            new() { Header = "单位", ValueType = ExcelValueType.Text, Width = 8 },
            new() { Header = "仓库", ValueType = ExcelValueType.Text, Width = 16 },
        };
        if (expandBatch)
        {
            columns.Add(new ExcelColumnModel { Header = "批次号", ValueType = ExcelValueType.Text, Width = 18 });
            columns.Add(new ExcelColumnModel { Header = "到期日", ValueType = ExcelValueType.Text, Width = 12 });
        }

        columns.Add(new ExcelColumnModel { Header = "当前库存", ValueType = ExcelValueType.Integer, Width = 12 });
        if (!expandBatch)
        {
            columns.Add(new ExcelColumnModel { Header = "安全阈值", ValueType = ExcelValueType.Integer, Width = 12 });
        }

        columns.Add(new ExcelColumnModel { Header = "最近变动时间", ValueType = ExcelValueType.DateTime, Width = 18 });
        columns.Add(new ExcelColumnModel { Header = "创建时间", ValueType = ExcelValueType.DateTime, Width = 18 });
        columns.Add(new ExcelColumnModel { Header = "创建人", ValueType = ExcelValueType.Text, Width = 14 });

        var sheet = new ExcelSheetModel
        {
            Name = ExportDomainNames.Inventory,
            Columns = columns,
            Rows = rows,
        };

        var workbook = new ExcelWorkbookModel { Sheets = [sheet] };

        return new ExportResultDto
        {
            FileName = ExportFileNames.Build(ExportDomainNames.Inventory, DateTimeOffset.UtcNow),
            Content = _excelExporter.Build(workbook),
        };
    }
}
