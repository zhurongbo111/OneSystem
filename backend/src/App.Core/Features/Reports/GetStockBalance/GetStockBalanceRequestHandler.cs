using App.Core.Abstractions;

namespace App.Core.Features.Reports.GetStockBalance;

/// <summary>
/// 库存余额表查询用例：启用商品按分类聚合（商品数 / 库存合计 / 零库存数 / 低库存数）与占比，附全量合计（只读）。
/// </summary>
public sealed class GetStockBalanceRequestHandler : IRequestHandler<GetStockBalanceRequest, ReportPageDto<StockBalanceItemDto, StockBalanceSummaryDto>>
{
    private readonly IReportQueryRepository _reportQueryRepository;

    /// <summary>
    /// 初始化库存余额表查询用例处理器
    /// </summary>
    /// <param name="reportQueryRepository">报表只读查询仓储</param>
    public GetStockBalanceRequestHandler(IReportQueryRepository reportQueryRepository)
    {
        _reportQueryRepository = reportQueryRepository;
    }

    /// <summary>
    /// 处理库存余额表查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ReportPageDto<StockBalanceItemDto, StockBalanceSummaryDto>> HandleAsync(
        GetStockBalanceRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total, summary) = await _reportQueryRepository.GetStockBalanceAsync(
            request.Keyword,
            request.CategoryId,
            request.WarehouseId,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new ReportPageDto<StockBalanceItemDto, StockBalanceSummaryDto>
        {
            // 占比分母为全量筛选结果的库存总量（不是当前页合计）
            Items = items.Select(item => ReportsDtoMapper.ToStockBalanceItemDto(item, summary.TotalQuantity)).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
            Summary = ReportsDtoMapper.ToStockBalanceSummaryDto(summary),
        };
    }
}
