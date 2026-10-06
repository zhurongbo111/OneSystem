using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Approvals.WithdrawApproval;

/// <summary>
/// 撤回待审批单据用例（specs/042-erp-approval/design.md §3.5）：
/// 记录存在（40400）→ 记录为待审批（40136）→ **提交人本人**（非本人按不存在处理，40400，不暴露他人记录）
/// → 同一事务内：被审批单据置「已撤回 + 已作废」（<see cref="ApprovalOrderCloser"/>）+ 记录写入撤回决定
/// → 操作日志 → 提交。**不做任何库存 / 流水 / 成本操作**；撤回是提交人自己的动作，不发站内信。
/// </summary>
public sealed class WithdrawApprovalRequestHandler : IRequestHandler<WithdrawApprovalRequest, ApprovalDetailDto>
{
    private readonly IApprovalRepository _approvalRepository;
    private readonly ApprovalOrderCloser _orderCloser;
    private readonly ApprovalDetailBuilder _detailBuilder;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化撤回待审批单据用例处理器
    /// </summary>
    public WithdrawApprovalRequestHandler(
        IApprovalRepository approvalRepository,
        ApprovalOrderCloser orderCloser,
        ApprovalDetailBuilder detailBuilder,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _approvalRepository = approvalRepository;
        _orderCloser = orderCloser;
        _detailBuilder = detailBuilder;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理撤回待审批单据请求
    /// </summary>
    /// <param name="request">撤回请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ApprovalDetailDto> HandleAsync(WithdrawApprovalRequest request, CancellationToken cancellationToken = default)
    {
        var approval = await _approvalRepository.GetByIdAsync(request.Id, cancellationToken);
        if (approval is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "审批记录不存在");
        }

        if (approval.Status != ApprovalStatus.Pending)
        {
            throw new BusinessException(ErrorCode.ApprovalStateInvalid, "该审批已处理，不能撤回");
        }

        var operatorId = _currentUser.UserId()
            ?? throw new BusinessException(ErrorCode.Unauthorized, "登录状态无效，请重新登录");
        if (approval.SubmittedBy != operatorId)
        {
            // 仅提交人本人可撤回；他人撤回会绕过审批职责，且不暴露他人记录（design.md §0.3）
            throw new BusinessException(ErrorCode.NotFound, "审批记录不存在");
        }

        var typeText = AuditText.SettlementOrderType(approval.OrderType);
        var now = DateTimeOffset.UtcNow;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _orderCloser.CloseAsync(approval, ApprovalStatus.Withdrawn, operatorId, cancellationToken);
            await _approvalRepository.UpdateDecisionAsync(
                approval.Id, ApprovalStatus.Withdrawn, operatorId, now, null, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("status", "审批状态", AuditText.ApprovalStatus(ApprovalStatus.Pending), AuditText.ApprovalStatus(ApprovalStatus.Withdrawn));
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Approval,
                Action = AuditAction.Approve,
                ResourceId = approval.Id,
                ResourceNo = approval.OrderNo,
                Summary = $"撤回{typeText} {approval.OrderNo}（往来：{approval.PartnerName}、{AuditSummary.Money(approval.Amount)}、单据一并作废）",
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

        var decided = await _approvalRepository.GetByIdAsync(approval.Id, cancellationToken) ?? approval;
        return await _detailBuilder.BuildAsync(decided, cancellationToken);
    }
}
