using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.Opportunities.GetOpportunityById;

/// <summary>
/// 商机详情查询用例：取商机（含负责人姓名），不存在报 40400
/// </summary>
public sealed class GetOpportunityByIdRequestHandler : IRequestHandler<GetOpportunityByIdRequest, OpportunityDetailDto>
{
    private readonly IOpportunityRepository _opportunityRepository;

    /// <summary>
    /// 初始化商机详情查询用例处理器
    /// </summary>
    public GetOpportunityByIdRequestHandler(IOpportunityRepository opportunityRepository)
    {
        _opportunityRepository = opportunityRepository;
    }

    /// <summary>
    /// 处理商机详情查询请求
    /// </summary>
    /// <param name="request">详情查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<OpportunityDetailDto> HandleAsync(GetOpportunityByIdRequest request, CancellationToken cancellationToken = default)
    {
        var detail = await _opportunityRepository.GetByIdAsync(request.Id, cancellationToken);
        if (detail is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "商机不存在");
        }

        return OpportunitiesDtoMapper.ToOpportunityDetailDto(detail);
    }
}
