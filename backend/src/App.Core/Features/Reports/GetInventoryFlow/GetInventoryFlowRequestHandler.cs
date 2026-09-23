using App.Core.Abstractions;

namespace App.Core.Features.Reports.GetInventoryFlow;

/// <summary>
/// 进销存报表查询用例：按期间 + 商品 / 分类筛选，返回期间入 / 出与期初 / 期末四列及全量合计（只读）。
/// </summary>
public sealed class GetInventoryFlowRequestHandler : IRequestHandler<GetInventoryFlowRequest, ReportPageDto<InventoryFlowItemDto, InventoryFlowSummaryDto>>
{
    private readonly IReportQueryRepository _reportQueryRepository;

    /// <summary>
    /// 初始化进销存报表查询用例处理器
    /// </summary>
    /// <param name="reportQueryRepository">报表只读查询仓储</param>
    public GetInventoryFlowRequestHandler(IReportQueryRepository reportQueryRepository)
    {
        _reportQueryRepository = reportQueryRepository;
    }

    /// <summary>
    /// 处理进销存报表查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ReportPageDto<InventoryFlowItemDto, InventoryFlowSummaryDto>> HandleAsync(
        GetInventoryFlowRequest request, CancellationToken cancellationToken = default)
    {
        // 期间必填由 Validator 保证（全局校验在分发前执行）
        var (items, total, summary) = await _reportQueryRepository.GetInventoryFlowAsync(
            request.Start!.Value,
            request.End!.Value,
            request.ProductId,
            request.CategoryId,
            request.OnlyChanged,
            request.WarehouseId,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new ReportPageDto<InventoryFlowItemDto, InventoryFlowSummaryDto>
        {
            Items = items.Select(ReportsDtoMapper.ToInventoryFlowItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
            Summary = ReportsDtoMapper.ToInventoryFlowSummaryDto(summary),
        };
    }
}
