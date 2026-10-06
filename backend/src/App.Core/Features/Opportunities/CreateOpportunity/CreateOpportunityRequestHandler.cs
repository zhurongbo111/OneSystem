using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Opportunities.CreateOpportunity;

/// <summary>
/// 新增商机用例（design.md §3.3 / §3.4）：
/// 来源线索 / 客户 / 负责人存在性校验（40400）→ 客户名称落快照 → 单号生成（OP + yyyyMMdd + 序号，冲突重试最多 3 次）
/// → 同一事务：插商机 + 记操作日志。**不触碰库存、库存流水与收付款**。
/// </summary>
public sealed class CreateOpportunityRequestHandler : IRequestHandler<CreateOpportunityRequest, OpportunityDetailDto>
{
    /// <summary>商机单号前缀（见 design.md §0.3）</summary>
    private const string OpportunityNoPrefix = "OP";

    /// <summary>单号冲突重试上限（含首次）</summary>
    private const int MaxOpportunityNoAttempts = 3;

    private readonly IOpportunityRepository _opportunityRepository;
    private readonly ILeadRepository _leadRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增商机用例处理器
    /// </summary>
    public CreateOpportunityRequestHandler(
        IOpportunityRepository opportunityRepository,
        ILeadRepository leadRepository,
        IPartnerRepository partnerRepository,
        IEmployeeRepository employeeRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _opportunityRepository = opportunityRepository;
        _leadRepository = leadRepository;
        _partnerRepository = partnerRepository;
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理新增商机请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<OpportunityDetailDto> HandleAsync(CreateOpportunityRequest request, CancellationToken cancellationToken = default)
    {
        // 查库约束：来源线索存在
        if (request.LeadId is not null)
        {
            var lead = await _leadRepository.GetByIdAsync(request.LeadId.Value, cancellationToken);
            if (lead is null)
            {
                throw new BusinessException(ErrorCode.NotFound, "来源线索不存在");
            }
        }

        // 查库约束：客户存在（可空；客户名称落快照，后续档案修改不影响历史商机）
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

        // 查库约束：负责人（员工）存在
        if (request.OwnerId is not null)
        {
            var owner = await _employeeRepository.GetByIdAsync(request.OwnerId.Value, cancellationToken);
            if (owner is null)
            {
                throw new BusinessException(ErrorCode.NotFound, "负责人不存在");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        // 事务：插商机 + 记日志；单号冲突（唯一索引）时回滚后重新生成单号重试
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
                    Name = request.Name.Trim(),
                    LeadId = request.LeadId,
                    PartnerId = request.PartnerId,
                    PartnerName = partnerName,
                    Amount = request.Amount,
                    Stage = request.Stage,
                    ExpectedCloseDate = request.ExpectedCloseDate,
                    OwnerId = request.OwnerId,
                    Remark = Normalize(request.Remark),
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = operatorId,
                    UpdatedBy = operatorId,
                };

                await _opportunityRepository.AddAsync(opportunity, cancellationToken);

                var changeBuilder = new AuditChangeBuilder()
                    .Add("opportunityNo", "商机单号", null, opportunity.OpportunityNo)
                    .Add("name", "商机名称", null, opportunity.Name)
                    .Add("partnerName", "客户", null, opportunity.PartnerName)
                    .Add("amount", "预计金额", null, AuditSummary.Money(opportunity.Amount))
                    .Add("stage", "阶段", null, AuditText.OpportunityStage(opportunity.Stage))
                    .Add("expectedCloseDate", "预计成交日期", null, opportunity.ExpectedCloseDate?.ToString("yyyy-MM-dd"))
                    .Add("remark", "备注", null, opportunity.Remark);
                await _auditLogger.RecordAsync(new AuditEntry
                {
                    Resource = AuditResource.Opportunity,
                    Action = AuditAction.Create,
                    ResourceId = opportunity.Id,
                    ResourceNo = opportunity.OpportunityNo,
                    Summary = $"创建商机 {opportunity.OpportunityNo}（{opportunity.Name}、{AuditSummary.Money(opportunity.Amount)}）",
                    Changes = changeBuilder.Build(),
                    ChangesTruncated = changeBuilder.Truncated,
                    UtcNow = now,
                }, cancellationToken);

                await _unitOfWork.CommitAsync(cancellationToken);

                var created = await _opportunityRepository.GetByIdAsync(opportunity.Id, cancellationToken);
                if (created is null)
                {
                    throw new BusinessException(ErrorCode.NotFound, "商机创建后读取失败");
                }

                return OpportunitiesDtoMapper.ToOpportunityDetailDto(created);
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

    /// <summary>可空文本归一：空白视为清空（落库 null），有值则 Trim（AGENTS.md §4.5）</summary>
    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
