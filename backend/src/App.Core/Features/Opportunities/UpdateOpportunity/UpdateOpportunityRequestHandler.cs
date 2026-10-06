using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Opportunities.UpdateOpportunity;

/// <summary>
/// 编辑商机用例（design.md §3.3，**全量覆盖**语义）：
/// 取商机（40400）→ 客户 / 负责人存在性校验（40400）→ 阶段流转规则（<see cref="OpportunityStageRules"/>）
/// → 更新字段（客户改则刷新名称快照）→ 记操作日志 → 提交。**不触碰库存、库存流水与收付款**。
/// </summary>
public sealed class UpdateOpportunityRequestHandler : IRequestHandler<UpdateOpportunityRequest, OpportunityDetailDto>
{
    private readonly IOpportunityRepository _opportunityRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化编辑商机用例处理器
    /// </summary>
    public UpdateOpportunityRequestHandler(
        IOpportunityRepository opportunityRepository,
        IPartnerRepository partnerRepository,
        IEmployeeRepository employeeRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _opportunityRepository = opportunityRepository;
        _partnerRepository = partnerRepository;
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理编辑商机请求
    /// </summary>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<OpportunityDetailDto> HandleAsync(UpdateOpportunityRequest request, CancellationToken cancellationToken = default)
    {
        var detail = await _opportunityRepository.GetByIdAsync(request.Id, cancellationToken);
        if (detail is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "商机不存在");
        }

        string? partnerName = null;
        if (request.PartnerId is not null)
        {
            var partner = await _partnerRepository.GetByIdAsync(request.PartnerId.Value, cancellationToken);
            if (partner is null)
            {
                throw new BusinessException(ErrorCode.NotFound, "客户不存在");
            }

            partnerName = partner.Name;
        }

        if (request.OwnerId is not null)
        {
            var owner = await _employeeRepository.GetByIdAsync(request.OwnerId.Value, cancellationToken);
            if (owner is null)
            {
                throw new BusinessException(ErrorCode.NotFound, "负责人不存在");
            }
        }

        var opportunity = detail.Opportunity;
        if (request.Stage != opportunity.Stage)
        {
            OpportunityStageRules.EnsureStageChangeAllowed(opportunity.Stage);
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        // 变更前后的旧值（日志用）
        var oldName = opportunity.Name;
        var oldPartnerName = opportunity.PartnerName;
        var oldAmount = opportunity.Amount;
        var oldStage = opportunity.Stage;
        var oldExpectedCloseDate = opportunity.ExpectedCloseDate;
        var oldRemark = opportunity.Remark;

        opportunity.Name = request.Name.Trim();
        opportunity.PartnerId = request.PartnerId;
        opportunity.PartnerName = partnerName;
        opportunity.Amount = request.Amount;
        opportunity.Stage = request.Stage;
        opportunity.ExpectedCloseDate = request.ExpectedCloseDate;
        opportunity.OwnerId = request.OwnerId;
        opportunity.Remark = Normalize(request.Remark);
        opportunity.UpdatedAt = now;
        opportunity.UpdatedBy = operatorId;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _opportunityRepository.UpdateAsync(opportunity, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("name", "商机名称", oldName, opportunity.Name)
                .Add("partnerName", "客户", oldPartnerName, opportunity.PartnerName)
                .Add("amount", "预计金额", AuditSummary.Money(oldAmount), AuditSummary.Money(opportunity.Amount))
                .Add("stage", "阶段", AuditText.OpportunityStage(oldStage), AuditText.OpportunityStage(opportunity.Stage))
                .Add("expectedCloseDate", "预计成交日期", oldExpectedCloseDate?.ToString("yyyy-MM-dd"), opportunity.ExpectedCloseDate?.ToString("yyyy-MM-dd"))
                .Add("remark", "备注", oldRemark, opportunity.Remark);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Opportunity,
                Action = AuditAction.Update,
                ResourceId = opportunity.Id,
                ResourceNo = opportunity.OpportunityNo,
                Summary = $"编辑商机 {opportunity.OpportunityNo}（{opportunity.Name}）",
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

    /// <summary>可空文本归一：空白视为清空（落库 null），有值则 Trim（AGENTS.md §4.5）</summary>
    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
