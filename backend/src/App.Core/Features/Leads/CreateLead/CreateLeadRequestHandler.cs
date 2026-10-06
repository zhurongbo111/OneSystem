using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Leads.CreateLead;

/// <summary>
/// 新增线索用例（design.md §3.3 / §3.4）：
/// 负责人存在性校验（40400）→ 单号生成（LD + yyyyMMdd + 序号，唯一索引冲突重试最多 3 次）
/// → 同一事务：插线索 + 记操作日志。**不触碰库存、库存流水与收付款**。
/// </summary>
public sealed class CreateLeadRequestHandler : IRequestHandler<CreateLeadRequest, LeadDetailDto>
{
    /// <summary>线索单号前缀（见 design.md §0.3）</summary>
    private const string LeadNoPrefix = "LD";

    /// <summary>单号冲突重试上限（含首次）</summary>
    private const int MaxLeadNoAttempts = 3;

    private readonly ILeadRepository _leadRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增线索用例处理器
    /// </summary>
    public CreateLeadRequestHandler(
        ILeadRepository leadRepository,
        IEmployeeRepository employeeRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _leadRepository = leadRepository;
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理新增线索请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<LeadDetailDto> HandleAsync(CreateLeadRequest request, CancellationToken cancellationToken = default)
    {
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

        // 事务：插线索 + 记日志；单号冲突（唯一索引）时回滚后重新生成单号重试
        for (var attempt = 1; ; attempt++)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var leadNo = await _leadRepository.GenerateNoAsync(LeadNoPrefix, now, cancellationToken);
                var lead = new Lead
                {
                    Id = Guid.NewGuid(),
                    LeadNo = leadNo,
                    Name = request.Name.Trim(),
                    Contact = Normalize(request.Contact),
                    Phone = Normalize(request.Phone),
                    Source = request.Source,
                    Status = request.Status,
                    OwnerId = request.OwnerId,
                    Remark = Normalize(request.Remark),
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = operatorId,
                    UpdatedBy = operatorId,
                };

                await _leadRepository.AddAsync(lead, cancellationToken);

                var changeBuilder = new AuditChangeBuilder()
                    .Add("leadNo", "线索单号", null, lead.LeadNo)
                    .Add("name", "线索名称", null, lead.Name)
                    .Add("contact", "联系人", null, lead.Contact)
                    .Add("phone", "联系电话", null, lead.Phone)
                    .Add("source", "来源", null, AuditText.LeadSource(lead.Source))
                    .Add("status", "状态", null, AuditText.LeadStatus(lead.Status))
                    .Add("remark", "备注", null, lead.Remark);
                await _auditLogger.RecordAsync(new AuditEntry
                {
                    Resource = AuditResource.Lead,
                    Action = AuditAction.Create,
                    ResourceId = lead.Id,
                    ResourceNo = lead.LeadNo,
                    Summary = $"创建线索 {lead.LeadNo}（{lead.Name}）",
                    Changes = changeBuilder.Build(),
                    ChangesTruncated = changeBuilder.Truncated,
                    UtcNow = now,
                }, cancellationToken);

                await _unitOfWork.CommitAsync(cancellationToken);

                var created = await _leadRepository.GetByIdAsync(lead.Id, cancellationToken);
                if (created is null)
                {
                    throw new BusinessException(ErrorCode.NotFound, "线索创建后读取失败");
                }

                return LeadsDtoMapper.ToLeadDetailDto(created);
            }
            catch (OrderNoConflictException) when (attempt < MaxLeadNoAttempts)
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
