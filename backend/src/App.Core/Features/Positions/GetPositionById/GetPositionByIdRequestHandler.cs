using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.Positions.GetPositionById;

/// <summary>
/// 岗位详情用例：按 id 查询，不存在返回 40400
/// </summary>
public sealed class GetPositionByIdRequestHandler : IRequestHandler<GetPositionByIdRequest, PositionDetailDto>
{
    private readonly IPositionRepository _positionRepository;

    /// <summary>
    /// 初始化岗位详情用例处理器
    /// </summary>
    public GetPositionByIdRequestHandler(IPositionRepository positionRepository)
    {
        _positionRepository = positionRepository;
    }

    /// <summary>
    /// 处理岗位详情请求
    /// </summary>
    /// <param name="request">详情请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PositionDetailDto> HandleAsync(GetPositionByIdRequest request, CancellationToken cancellationToken = default)
    {
        var position = await _positionRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "岗位不存在");

        return PositionDtoMapper.ToPositionDetailDto(position);
    }
}
