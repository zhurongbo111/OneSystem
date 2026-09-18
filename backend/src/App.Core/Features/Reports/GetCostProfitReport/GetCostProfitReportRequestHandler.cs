using App.Core.Abstractions;

namespace App.Core.Features.Reports.GetCostProfitReport;

/// <summary>
/// 成本与毛利报表查询用例（erp-cost）：按期间 + 商品 / 分类 + 分组维度聚合销售收入与销售成本，
/// 出参层计算毛利与毛利率（design §0.3），合计为全量筛选结果口径。
/// </summary>
public sealed class GetCostProfitReportRequestHandler : IRequestHandler<GetCostProfitReportRequest, ReportPageDto<CostProfitItemDto, CostProfitTotalDto>>
{
    private readonly IReportQueryRepository _reportQueryRepository;

    /// <summary>
    /// 初始化成本与毛利报表查询用例处理器
    /// </summary>
    /// <param name="reportQueryRepository">报表只读查询仓储</param>
    public GetCostProfitReportRequestHandler(IReportQueryRepository reportQueryRepository)
    {
        _reportQueryRepository = reportQueryRepository;
    }

    /// <summary>
    /// 处理成本与毛利报表查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ReportPageDto<CostProfitItemDto, CostProfitTotalDto>> HandleAsync(
        GetCostProfitReportRequest request, CancellationToken cancellationToken = default)
    {
        // 期间必填由 Validator 保证（全局校验在分发前执行）
        var (items, total, summary) = await _reportQueryRepository.GetCostProfitAsync(
            request.Start!.Value,
            request.End!.Value,
            request.ProductId,
            request.CategoryId,
            request.GroupBy,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new ReportPageDto<CostProfitItemDto, CostProfitTotalDto>
        {
            Items = items.Select(ReportsDtoMapper.ToCostProfitItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
            Summary = ReportsDtoMapper.ToCostProfitTotalDto(summary),
        };
    }
}
