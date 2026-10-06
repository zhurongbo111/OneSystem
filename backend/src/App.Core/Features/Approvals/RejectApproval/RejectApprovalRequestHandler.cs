using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Approvals.RejectApproval;

/// <summary>
/// 审批驳回用例（specs/042-erp-approval/design.md §3.5）：
/// 记录存在（40400）→ 记录为待审批（40136）→ 不可自审（40137）→ 意见必填（格式校验器 40000）
/// → 同一事务内：被审批单据置「已驳回 + 已作废」（<see cref="ApprovalOrderCloser"/>）+ 记录写入驳回决定
/// → 操作日志 → 提交 → 通知提交人。**不做任何库存 / 流水 / 成本操作**（单据从未生效，无需回冲）。
/// </summary>
public sealed class RejectApprovalRequestHandler : IRequestHandler<RejectApprovalRequest, ApprovalDetailDto>
{
    private readonly IApprovalRepository _approvalRepository;
    private readonly ApprovalOrderCloser _orderCloser;
    private readonly ApprovalDetailBuilder _detailBuilder;
    private readonly ApprovalNotifier _approvalNotifier;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化审批驳回用例处理器
    /// </summary>
    public RejectApprovalRequestHandler(
        IApprovalRepository approvalRepository,
        ApprovalOrderCloser orderCloser,
        ApprovalDetailBuilder detailBuilder,
        ApprovalNotifier approvalNotifier,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _approvalRepository = approvalRepository;
        _orderCloser = orderCloser;
        _detailBuilder = detailBuilder;
        _approvalNotifier = approvalNotifier;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理审批驳回请求
    /// </summary>
    /// <param name="request">驳回请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ApprovalDetailDto> HandleAsync(RejectApprovalRequest request, CancellationToken cancellationToken = default)
    {
        var approval = await _approvalRepository.GetByIdAsync(request.Id, cancellationToken);
        if (approval is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "审批记录不存在");
        }

        if (approval.Status != ApprovalStatus.Pending)
        {
            throw new BusinessException(ErrorCode.ApprovalStateInvalid, "该审批已处理，不能重复驳回");
        }

        var operatorId = _currentUser.UserId()
            ?? throw new BusinessException(ErrorCode.Unauthorized, "登录状态无效，请重新登录");
        if (approval.SubmittedBy == operatorId)
        {
            throw new BusinessException(ErrorCode.ApprovalSelfForbidden, "不能审批自己提交的单据");
        }

        var remark = request.Remark!.Trim();
        var typeText = AuditText.SettlementOrderType(approval.OrderType);
        var now = DateTimeOffset.UtcNow;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _orderCloser.CloseAsync(approval, ApprovalStatus.Rejected, operatorId, cancellationToken);
            await _approvalRepository.UpdateDecisionAsync(
                approval.Id, ApprovalStatus.Rejected, operatorId, now, remark, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("status", "审批状态", AuditText.ApprovalStatus(ApprovalStatus.Pending), AuditText.ApprovalStatus(ApprovalStatus.Rejected))
                .Add("decisionRemark", "审批意见", null, remark);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Approval,
                Action = AuditAction.Approve,
                ResourceId = approval.Id,
                ResourceNo = approval.OrderNo,
                Summary = $"驳回{typeText} {approval.OrderNo}（往来：{approval.PartnerName}、{AuditSummary.Money(approval.Amount)}、单据一并作废）｜意见：{remark}",
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
        await _approvalNotifier.NotifyDecidedAsync(decided, "驳回", cancellationToken);
        return await _detailBuilder.BuildAsync(decided, cancellationToken);
    }
}
