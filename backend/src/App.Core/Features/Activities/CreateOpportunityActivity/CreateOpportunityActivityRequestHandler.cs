using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Activities.CreateOpportunityActivity;

/// <summary>
/// 新增商机跟进活动用例（design.md §3.3）：
/// 商机存在性校验（40400）→ 追加一条归属「商机 + 商机 id」的活动 → 记操作日志（同一事务）。
/// 活动**只增不改不删**；不触碰库存、库存流水与收付款。
/// </summary>
public sealed class CreateOpportunityActivityRequestHandler : IRequestHandler<CreateOpportunityActivityRequest, ActivityItemDto>
{
    private readonly IOpportunityRepository _opportunityRepository;
    private readonly IActivityRepository _activityRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增商机跟进活动用例处理器
    /// </summary>
    public CreateOpportunityActivityRequestHandler(
        IOpportunityRepository opportunityRepository,
        IActivityRepository activityRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _opportunityRepository = opportunityRepository;
        _activityRepository = activityRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理新增商机跟进活动请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ActivityItemDto> HandleAsync(CreateOpportunityActivityRequest request, CancellationToken cancellationToken = default)
    {
        var opportunity = await _opportunityRepository.GetByIdAsync(request.OpportunityId, cancellationToken);
        if (opportunity is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "商机不存在");
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            BizType = ActivityBizType.Opportunity,
            BizId = opportunity.Opportunity.Id,
            Type = request.Type,
            Content = request.Content.Trim(),
            ActivityTime = request.ActivityTime,
            CreatedAt = now,
            CreatedBy = operatorId,
        };

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _activityRepository.AddAsync(activity, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("bizType", "归属类型", null, "商机")
                .Add("bizNo", "归属单据", null, opportunity.Opportunity.OpportunityNo)
                .Add("type", "跟进方式", null, AuditText.ActivityType(activity.Type))
                .Add("content", "跟进内容", null, activity.Content)
                .Add("activityTime", "跟进时间", null, AuditSummary.Date(activity.ActivityTime));
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Activity,
                Action = AuditAction.Create,
                ResourceId = activity.Id,
                ResourceNo = opportunity.Opportunity.OpportunityNo,
                Summary = $"商机 {opportunity.Opportunity.OpportunityNo} 新增{AuditText.ActivityType(activity.Type)}跟进记录",
                Changes = changeBuilder.Build(),
                ChangesTruncated = changeBuilder.Truncated,
                UtcNow = now,
            }, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        return ActivitiesDtoMapper.ToActivityItemDto(new ActivityItem
        {
            Activity = activity,
            RecorderName = _currentUser.DisplayName,
        });
    }
}
