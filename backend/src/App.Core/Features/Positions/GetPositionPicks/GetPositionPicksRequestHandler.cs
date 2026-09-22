using App.Core.Abstractions;

namespace App.Core.Features.Positions.GetPositionPicks;

/// <summary>
/// 岗位选择用例：仅启用岗位（仓储已过滤），全量返回
/// </summary>
public sealed class GetPositionPicksRequestHandler : IRequestHandler<GetPositionPicksRequest, IReadOnlyList<PositionPickDto>>
{
    private readonly IPositionRepository _positionRepository;

    /// <summary>
    /// 初始化岗位选择用例处理器
    /// </summary>
    public GetPositionPicksRequestHandler(IPositionRepository positionRepository)
    {
        _positionRepository = positionRepository;
    }

    /// <summary>
    /// 处理岗位选择请求
    /// </summary>
    /// <param name="request">选择请求（空参数）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IReadOnlyList<PositionPickDto>> HandleAsync(GetPositionPicksRequest request, CancellationToken cancellationToken = default)
    {
        var items = await _positionRepository.GetPickListAsync(cancellationToken);
        return items.Select(PositionDtoMapper.ToPositionPickDto).ToList();
    }
}
