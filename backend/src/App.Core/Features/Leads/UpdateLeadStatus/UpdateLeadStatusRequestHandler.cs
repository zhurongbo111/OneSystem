using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Leads.UpdateLeadStatus;

/// <summary>
/// 线索状态流转用例（design.md §3.3 / §3.4）：
/// 取线索（40400）→ 状态流转规则（终态拒绝 / 已转化仅由转商机产生，40168）→ 更新状态 → 记操作日志。
/// </summary>
public sealed class UpdateLeadStatusRequestHandler : IRequestHandler<UpdateLeadStatusRequest, LeadDetailDto>
{
    private readonly ILeadRepository _leadRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化线索状态流转用例处理器
    /// </summary>
    public UpdateLeadStatusRequestHandler(
        ILeadRepository leadRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _leadRepository = leadRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理线索状态流转请求
    /// </summary>
    /// <param name="request">状态流转请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<LeadDetailDto> HandleAsync(UpdateLeadStatusRequest request, CancellationToken cancellationToken = default)
    {
        var detail = await _leadRepository.GetByIdAsync(request.Id, cancellationToken);
        if (detail is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "线索不存在");
        }

        var lead = detail.Lead;
        var oldStatus = lead.Status;
        LeadStatusRules.EnsureChangeAllowed(oldStatus, request.Status);

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        lead.Status = request.Status;
        lead.UpdatedAt = now;
        lead.UpdatedBy = operatorId;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _leadRepository.UpdateAsync(lead, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("status", "状态", AuditText.LeadStatus(oldStatus), AuditText.LeadStatus(lead.Status));
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Lead,
                Action = AuditAction.StatusChange,
                ResourceId = lead.Id,
                ResourceNo = lead.LeadNo,
                Summary = $"线索 {lead.LeadNo} 状态变更为{AuditText.LeadStatus(lead.Status)}",
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

        var updated = await _leadRepository.GetByIdAsync(lead.Id, cancellationToken);
        if (updated is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "线索更新后读取失败");
        }

        return LeadsDtoMapper.ToLeadDetailDto(updated);
    }
}
