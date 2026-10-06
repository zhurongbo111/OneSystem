using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Opportunities.UpdateOpportunityStage;

/// <summary>
/// 商机阶段推进用例（design.md §3.3 / §3.4）：
/// 取商机（40400）→ 当前为终态（赢单 / 输单）则 40169 → 更新阶段 → 记操作日志。
/// </summary>
public sealed class UpdateOpportunityStageRequestHandler : IRequestHandler<UpdateOpportunityStageRequest, OpportunityDetailDto>
{
    private readonly IOpportunityRepository _opportunityRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化商机阶段推进用例处理器
    /// </summary>
    public UpdateOpportunityStageRequestHandler(
        IOpportunityRepository opportunityRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _opportunityRepository = opportunityRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理商机阶段推进请求
    /// </summary>
    /// <param name="request">阶段推进请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<OpportunityDetailDto> HandleAsync(UpdateOpportunityStageRequest request, CancellationToken cancellationToken = default)
    {
        var detail = await _opportunityRepository.GetByIdAsync(request.Id, cancellationToken);
        if (detail is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "商机不存在");
        }

        var opportunity = detail.Opportunity;
        var oldStage = opportunity.Stage;

        // 终态（赢单 / 输单）后不可再改阶段；目标与当前一致视为幂等，不触发终态限制
        if (request.Stage != oldStage)
        {
            OpportunityStageRules.EnsureStageChangeAllowed(oldStage);
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        opportunity.Stage = request.Stage;
        opportunity.UpdatedAt = now;
        opportunity.UpdatedBy = operatorId;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _opportunityRepository.UpdateAsync(opportunity, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("stage", "阶段", AuditText.OpportunityStage(oldStage), AuditText.OpportunityStage(opportunity.Stage));
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Opportunity,
                Action = AuditAction.StatusChange,
                ResourceId = opportunity.Id,
                ResourceNo = opportunity.OpportunityNo,
                Summary = $"商机 {opportunity.OpportunityNo} 阶段变更为{AuditText.OpportunityStage(opportunity.Stage)}",
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

        var updated = await _opportunityRepository.GetByIdAsync(opportunity.Id, cancellationToken);
        if (updated is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "商机更新后读取失败");
        }

        return OpportunitiesDtoMapper.ToOpportunityDetailDto(updated);
    }
}
