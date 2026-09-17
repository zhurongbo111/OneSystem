using App.Core.Abstractions;

namespace App.Core.Features.Reports.GetSalesSummary;

/// <summary>
/// 销售汇总查询用例：期间内未作废销售出库单与销售退货单按客户 / 商品维度聚合，输出净额（只读）。
/// </summary>
public sealed class GetSalesSummaryRequestHandler : IRequestHandler<GetSalesSummaryRequest, ReportPageDto<SalesSummaryItemDto, SalesSummaryTotalDto>>
{
    private readonly IReportQueryRepository _reportQueryRepository;

    /// <summary>
    /// 初始化销售汇总查询用例处理器
    /// </summary>
    /// <param name="reportQueryRepository">报表只读查询仓储</param>
    public GetSalesSummaryRequestHandler(IReportQueryRepository reportQueryRepository)
    {
        _reportQueryRepository = reportQueryRepository;
    }

    /// <summary>
    /// 处理销售汇总查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ReportPageDto<SalesSummaryItemDto, SalesSummaryTotalDto>> HandleAsync(
        GetSalesSummaryRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total, summary) = await _reportQueryRepository.GetSalesSummaryAsync(
            request.Start!.Value,
            request.End!.Value,
            request.PartnerId,
            request.GroupBy == SummaryGroupBy.Product,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new ReportPageDto<SalesSummaryItemDto, SalesSummaryTotalDto>
        {
            Items = items.Select(ReportsDtoMapper.ToSalesSummaryItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
            Summary = ReportsDtoMapper.ToSalesSummaryTotalDto(summary),
        };
    }
}
