using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Activities.GetLeadActivities;

/// <summary>
/// 线索跟进活动查询用例：线索存在性校验（40400）→ 按归属（线索 + id）取活动，跟进时间倒序
/// </summary>
public sealed class GetLeadActivitiesRequestHandler : IRequestHandler<GetLeadActivitiesRequest, IReadOnlyList<ActivityItemDto>>
{
    private readonly ILeadRepository _leadRepository;
    private readonly IActivityRepository _activityRepository;

    /// <summary>
    /// 初始化线索跟进活动查询用例处理器
    /// </summary>
    public GetLeadActivitiesRequestHandler(ILeadRepository leadRepository, IActivityRepository activityRepository)
    {
        _leadRepository = leadRepository;
        _activityRepository = activityRepository;
    }

    /// <summary>
    /// 处理线索跟进活动查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IReadOnlyList<ActivityItemDto>> HandleAsync(GetLeadActivitiesRequest request, CancellationToken cancellationToken = default)
    {
        var lead = await _leadRepository.GetByIdAsync(request.LeadId, cancellationToken);
        if (lead is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "线索不存在");
        }

        var items = await _activityRepository.GetByBizAsync(ActivityBizType.Lead, request.LeadId, cancellationToken);
        return items.Select(ActivitiesDtoMapper.ToActivityItemDto).ToList();
    }
}
