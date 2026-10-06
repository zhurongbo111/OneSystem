using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Leads.ConvertLead;

/// <summary>
/// 线索转商机用例（design.md §0.2 / §3.4）：
/// 取线索（40400）→ 非终态校验（40168）→ **同一事务**内：创建商机（名称取线索名、客户空、负责人同线索）
/// + 回写线索（状态置已转化、回填商机 id / 单号）→ 提交。
/// 一条线索**只能转一次**；转商机**不动库存、不写流水、不产生应收**。
/// </summary>
public sealed class ConvertLeadRequestHandler : IRequestHandler<ConvertLeadRequest, ConvertLeadResultDto>
{
    /// <summary>商机单号前缀（见 design.md §0.3）</summary>
    private const string OpportunityNoPrefix = "OP";

    /// <summary>单号冲突重试上限（含首次）</summary>
    private const int MaxOpportunityNoAttempts = 3;

    private readonly ILeadRepository _leadRepository;
    private readonly IOpportunityRepository _opportunityRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化线索转商机用例处理器
    /// </summary>
    public ConvertLeadRequestHandler(
        ILeadRepository leadRepository,
        IOpportunityRepository opportunityRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _leadRepository = leadRepository;
        _opportunityRepository = opportunityRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理线索转商机请求
    /// </summary>
    /// <param name="request">转商机请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ConvertLeadResultDto> HandleAsync(ConvertLeadRequest request, CancellationToken cancellationToken = default)
    {
        var detail = await _leadRepository.GetByIdAsync(request.Id, cancellationToken);
        if (detail is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "线索不存在");
        }

        var lead = detail.Lead;
        LeadStatusRules.EnsureConvertible(lead.Status);
        var oldStatus = lead.Status;

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        // 事务：建商机 + 回写线索；商机单号唯一索引冲突时回滚重试
        for (var attempt = 1; ; attempt++)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var opportunityNo = await _opportunityRepository.GenerateNoAsync(OpportunityNoPrefix, now, cancellationToken);
                var opportunity = new Opportunity
                {
                    Id = Guid.NewGuid(),
                    OpportunityNo = opportunityNo,
                    Name = lead.Name,
                    LeadId = lead.Id,
                    PartnerId = null,
                    PartnerName = null,
                    Amount = 0m,
                    Stage = OpportunityStage.Initial,
                    ExpectedCloseDate = null,
                    OwnerId = lead.OwnerId,
                    Remark = lead.Remark,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = operatorId,
                    UpdatedBy = operatorId,
                };

                await _opportunityRepository.AddAsync(opportunity, cancellationToken);

                // 回写线索：置「已转化」（终态）并回填商机 id / 单号，锁定后不可再转
                lead.Status = LeadStatus.Converted;
                lead.OpportunityId = opportunity.Id;
                lead.OpportunityNo = opportunity.OpportunityNo;
                lead.UpdatedAt = now;
                lead.UpdatedBy = operatorId;
                await _leadRepository.UpdateAsync(lead, cancellationToken);

                var changeBuilder = new AuditChangeBuilder()
                    .Add("status", "状态", AuditText.LeadStatus(oldStatus), AuditText.LeadStatus(LeadStatus.Converted))
                    .Add("opportunityNo", "转出商机", null, opportunity.OpportunityNo);
                await _auditLogger.RecordAsync(new AuditEntry
                {
                    Resource = AuditResource.Lead,
                    Action = AuditAction.Update,
                    ResourceId = lead.Id,
                    ResourceNo = lead.LeadNo,
                    Summary = $"线索 {lead.LeadNo} 转商机 {opportunity.OpportunityNo}（{lead.Name}）",
                    Changes = changeBuilder.Build(),
                    ChangesTruncated = changeBuilder.Truncated,
                    UtcNow = now,
                }, cancellationToken);

                await _unitOfWork.CommitAsync(cancellationToken);

                return LeadsDtoMapper.ToConvertLeadResultDto(opportunity);
            }
            catch (OrderNoConflictException) when (attempt < MaxOpportunityNoAttempts)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                continue;
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}
