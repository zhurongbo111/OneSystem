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
            1,
            ExportFieldConstraints.MaxRows + 1,
            cancellationToken);
        ExportGuards.EnsureWithinRowLimit(items.Count);

        var creatorNames = await _userRepository.GetDisplayNamesByIdsAsync(
            items.Where(i => i.CreatedBy is not null).Select(i => i.CreatedBy!.Value).Distinct().ToList(),
            cancellationToken);

        var rows = new List<IReadOnlyList<object?>>(items.Count);
        foreach (var item in items)
        {
            rows.Add(new object?[]
            {
                item.Code,
                item.Name,
                item.CategoryName,
                item.Unit,
                item.StockQuantity,
                item.SafetyStock,
                item.UpdatedAt,
                item.CreatedAt,
                item.CreatedBy is not null && creatorNames.TryGetValue(item.CreatedBy.Value, out var creator) ? creator : string.Empty,
            });
        }

        var sheet = new ExcelSheetModel
        {
            Name = ExportDomainNames.Inventory,
            Columns =
            [
                new ExcelColumnModel { Header = "编码", ValueType = ExcelValueType.Text, Width = 18 },
                new ExcelColumnModel { Header = "名称", ValueType = ExcelValueType.Text, Width = 24 },
                new ExcelColumnModel { Header = "分类", ValueType = ExcelValueType.Text, Width = 14 },
                new ExcelColumnModel { Header = "单位", ValueType = ExcelValueType.Text, Width = 8 },
                new ExcelColumnModel { Header = "当前库存", ValueType = ExcelValueType.Integer, Width = 12 },
                new ExcelColumnModel { Header = "安全阈值", ValueType = ExcelValueType.Integer, Width = 12 },
                new ExcelColumnModel { Header = "最近变动时间", ValueType = ExcelValueType.DateTime, Width = 18 },
                new ExcelColumnModel { Header = "创建时间", ValueType = ExcelValueType.DateTime, Width = 18 },
                new ExcelColumnModel { Header = "创建人", ValueType = ExcelValueType.Text, Width = 14 },
            ],
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
