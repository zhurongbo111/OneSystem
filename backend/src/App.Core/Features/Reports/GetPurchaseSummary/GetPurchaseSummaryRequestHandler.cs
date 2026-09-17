using App.Core.Abstractions;

namespace App.Core.Features.Reports.GetPurchaseSummary;

/// <summary>
/// 采购汇总查询用例：期间内未作废采购入库单与采购退货单按往来单位 / 商品维度聚合，输出净额（只读）。
/// </summary>
public sealed class GetPurchaseSummaryRequestHandler : IRequestHandler<GetPurchaseSummaryRequest, ReportPageDto<PurchaseSummaryItemDto, PurchaseSummaryTotalDto>>
{
    private readonly IReportQueryRepository _reportQueryRepository;

    /// <summary>
    /// 初始化采购汇总查询用例处理器
    /// </summary>
    /// <param name="reportQueryRepository">报表只读查询仓储</param>
    public GetPurchaseSummaryRequestHandler(IReportQueryRepository reportQueryRepository)
    {
        _reportQueryRepository = reportQueryRepository;
    }

    /// <summary>
    /// 处理采购汇总查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ReportPageDto<PurchaseSummaryItemDto, PurchaseSummaryTotalDto>> HandleAsync(
        GetPurchaseSummaryRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total, summary) = await _reportQueryRepository.GetPurchaseSummaryAsync(
            request.Start!.Value,
            request.End!.Value,
            request.PartnerId,
            request.GroupBy == SummaryGroupBy.Product,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new ReportPageDto<PurchaseSummaryItemDto, PurchaseSummaryTotalDto>
        {
            Items = items.Select(ReportsDtoMapper.ToPurchaseSummaryItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
            Summary = ReportsDtoMapper.ToPurchaseSummaryTotalDto(summary),
        };
    }
}
