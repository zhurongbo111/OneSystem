using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.Positions.GetPositions;

/// <summary>
/// 岗位分页列表用例：按关键词 / 状态筛选后分页查询，直接依赖仓储，无 Service 层
/// </summary>
public sealed class GetPositionsRequestHandler : IRequestHandler<GetPositionsRequest, PagedResult<PositionListItemDto>>
{
    private readonly IPositionRepository _positionRepository;

    /// <summary>
    /// 初始化岗位分页列表用例处理器
    /// </summary>
    public GetPositionsRequestHandler(IPositionRepository positionRepository)
    {
        _positionRepository = positionRepository;
    }

    /// <summary>
    /// 处理岗位分页列表请求
    /// </summary>
    /// <param name="request">列表请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<PositionListItemDto>> HandleAsync(GetPositionsRequest request, CancellationToken cancellationToken = default)
    {
        var status = request.Status is null ? (PositionStatus?)null : (PositionStatus)request.Status.Value;
        var (items, total) = await _positionRepository.GetPagedAsync(
            request.Keyword,
            status,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResult<PositionListItemDto>
        {
            Items = items.Select(PositionDtoMapper.ToPositionListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
