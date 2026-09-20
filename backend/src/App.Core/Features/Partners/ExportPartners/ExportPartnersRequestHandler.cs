using App.Core.Abstractions;
using App.Core.Exports;

namespace App.Core.Features.Partners.ExportPartners;

/// <summary>
/// 往来单位列表导出用例（erp-export）：复用往来单位列表的筛选与仓储查询取全量，
/// 导出列 = 列表列定义（去序号 / 操作列）+ 创建信息，输出单工作表 xlsx
/// </summary>
public sealed class ExportPartnersRequestHandler : IRequestHandler<ExportPartnersRequest, ExportResultDto>
{
    private readonly IPartnerRepository _partnerRepository;
    private readonly IUserRepository _userRepository;
    private readonly IExcelExporter _excelExporter;

    /// <summary>
    /// 初始化往来单位列表导出用例处理器
    /// </summary>
    public ExportPartnersRequestHandler(
        IPartnerRepository partnerRepository,
        IUserRepository userRepository,
        IExcelExporter excelExporter)
    {
        _partnerRepository = partnerRepository;
        _userRepository = userRepository;
        _excelExporter = excelExporter;
    }

    /// <summary>
    /// 处理往来单位列表导出请求
    /// </summary>
    /// <param name="request">导出请求（筛选参数与列表一致）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ExportResultDto> HandleAsync(ExportPartnersRequest request, CancellationToken cancellationToken = default)
    {
        // 按上限 + 1 取数：超出上限即明确报错，不静默截断（分页参数不参与，导出当前筛选全量）
        var (items, _) = await _partnerRepository.GetPagedAsync(
            request.Keyword,
            request.Type,
            request.Status,
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
                item.Name,
                ExportLabels.ToText(item.Type),
                item.Contact,
                item.Phone,
                item.Address,
                ExportLabels.ToText(item.Status),
                item.CreatedAt,
                item.CreatedBy is not null && creatorNames.TryGetValue(item.CreatedBy.Value, out var creator) ? creator : string.Empty,
            });
        }

        var sheet = new ExcelSheetModel
        {
            Name = ExportDomainNames.Partners,
            Columns =
            [
                new ExcelColumnModel { Header = "名称", ValueType = ExcelValueType.Text, Width = 24 },
                new ExcelColumnModel { Header = "类型", ValueType = ExcelValueType.Text, Width = 12 },
                new ExcelColumnModel { Header = "联系人", ValueType = ExcelValueType.Text, Width = 14 },
                new ExcelColumnModel { Header = "电话", ValueType = ExcelValueType.Text, Width = 16 },
                new ExcelColumnModel { Header = "地址", ValueType = ExcelValueType.Text, Width = 24 },
                new ExcelColumnModel { Header = "状态", ValueType = ExcelValueType.Text, Width = 10 },
                new ExcelColumnModel { Header = "创建时间", ValueType = ExcelValueType.DateTime, Width = 18 },
                new ExcelColumnModel { Header = "创建人", ValueType = ExcelValueType.Text, Width = 14 },
            ],
            Rows = rows,
        };

        var workbook = new ExcelWorkbookModel { Sheets = [sheet] };

        return new ExportResultDto
        {
            FileName = ExportFileNames.Build(ExportDomainNames.Partners, DateTimeOffset.UtcNow),
            Content = _excelExporter.Build(workbook),
        };
    }
}
