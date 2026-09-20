using App.Core.Abstractions;
using App.Core.Exports;

namespace App.Core.Features.Settlements.ExportSettlements;

/// <summary>
/// 收付款单导出用例（erp-export）：复用列表筛选取全量单据 + 一次批量取核销明细分组，
/// 输出「单据 + 核销明细」两个工作表（明细首列为所属单号）；作废单据照常导出，便于对账核对
/// </summary>
public sealed class ExportSettlementsRequestHandler : IRequestHandler<ExportSettlementsRequest, ExportResultDto>
{
    private readonly ISettlementRepository _settlementRepository;
    private readonly IUserRepository _userRepository;
    private readonly IExcelExporter _excelExporter;

    /// <summary>
    /// 初始化收付款单导出用例处理器
    /// </summary>
    public ExportSettlementsRequestHandler(
        ISettlementRepository settlementRepository,
        IUserRepository userRepository,
        IExcelExporter excelExporter)
    {
        _settlementRepository = settlementRepository;
        _userRepository = userRepository;
        _excelExporter = excelExporter;
    }

    /// <summary>
    /// 处理收付款单导出请求
    /// </summary>
    /// <param name="request">导出请求（筛选参数与列表一致）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ExportResultDto> HandleAsync(ExportSettlementsRequest request, CancellationToken cancellationToken = default)
    {
        // 按上限 + 1 取数：超出上限即明确报错，不静默截断（分页参数不参与，导出当前筛选全量）
        var (settlements, _) = await _settlementRepository.GetPagedAsync(
            request.Keyword,
            request.Type,
            request.PartnerId,
            request.Method,
            request.Start,
            request.End,
            1,
            ExportFieldConstraints.MaxRows + 1,
            cancellationToken);
        ExportGuards.EnsureWithinRowLimit(settlements.Count);

        // 核销明细一次批量取回（避免逐单查询 N+1），再按单据 id 分组
        var settlementIds = settlements.Select(s => s.Id).ToList();
        var items = await _settlementRepository.GetItemsBySettlementIdsAsync(settlementIds, cancellationToken);
        ExportGuards.EnsureWithinRowLimit(items.Count);

        var creatorNames = await _userRepository.GetDisplayNamesByIdsAsync(
            settlements.Where(s => s.CreatedBy is not null).Select(s => s.CreatedBy!.Value).Distinct().ToList(),
            cancellationToken);

        var itemsBySettlement = items.ToLookup(i => i.SettlementId);

        var settlementRows = new List<IReadOnlyList<object?>>(settlements.Count);
        var itemRows = new List<IReadOnlyList<object?>>(items.Count);
        foreach (var settlement in settlements)
        {
            settlementRows.Add(new object?[]
            {
                settlement.SettlementNo,
                ExportLabels.ToText(settlement.Type),
                settlement.PartnerName,
                settlement.SettlementDate,
                settlement.TotalAmount,
                ExportLabels.ToText(settlement.Method),
                ExportLabels.ToText(settlement.Status),
                settlement.CreatedAt,
                settlement.CreatedBy is not null && creatorNames.TryGetValue(settlement.CreatedBy.Value, out var creator) ? creator : string.Empty,
            });

            foreach (var item in itemsBySettlement[settlement.Id])
            {
                itemRows.Add(new object?[]
                {
                    settlement.SettlementNo,
                    ExportLabels.ToText(item.OrderType),
                    item.OrderNo,
                    item.OrderDate,
                    item.OrderTotalAmount,
                    item.Amount,
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
                        new ExcelColumnModel { Header = "类型", ValueType = ExcelValueType.Text, Width = 10 },
                        new ExcelColumnModel { Header = "往来单位", ValueType = ExcelValueType.Text, Width = 24 },
                        new ExcelColumnModel { Header = "收付日期", ValueType = ExcelValueType.Date, Width = 14 },
                        new ExcelColumnModel { Header = "总额", ValueType = ExcelValueType.Decimal, Width = 14 },
                        new ExcelColumnModel { Header = "方式", ValueType = ExcelValueType.Text, Width = 12 },
                        new ExcelColumnModel { Header = "状态", ValueType = ExcelValueType.Text, Width = 10 },
                        new ExcelColumnModel { Header = "创建时间", ValueType = ExcelValueType.DateTime, Width = 18 },
                        new ExcelColumnModel { Header = "创建人", ValueType = ExcelValueType.Text, Width = 14 },
                    ],
                    Rows = settlementRows,
                },
                new ExcelSheetModel
                {
                    Name = ExportDomainNames.DetailSheet,
                    Columns =
                    [
                        new ExcelColumnModel { Header = "单号", ValueType = ExcelValueType.Text, Width = 20 },
                        new ExcelColumnModel { Header = "核销单据类型", ValueType = ExcelValueType.Text, Width = 14 },
                        new ExcelColumnModel { Header = "核销单号", ValueType = ExcelValueType.Text, Width = 20 },
                        new ExcelColumnModel { Header = "单据日期", ValueType = ExcelValueType.Date, Width = 14 },
                        new ExcelColumnModel { Header = "单据金额", ValueType = ExcelValueType.Decimal, Width = 14 },
                        new ExcelColumnModel { Header = "本次核销金额", ValueType = ExcelValueType.Decimal, Width = 16 },
                    ],
                    Rows = itemRows,
                },
            ],
        };

        return new ExportResultDto
        {
            FileName = ExportFileNames.Build(ExportDomainNames.Settlements, DateTimeOffset.UtcNow),
            Content = _excelExporter.Build(workbook),
        };
    }
}
