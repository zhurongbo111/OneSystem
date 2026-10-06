using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.PurchaseReceipts;
using App.Core.Features.PurchaseReturns;
using App.Core.Features.SalesReturns;
using App.Core.Features.SalesShipments;

namespace App.Core.Features.Approvals.ApproveOrder;

/// <summary>
/// 审批通过用例（specs/042-erp-approval/design.md §3.5）：
/// 记录存在（40400）→ 记录为待审批（40136）→ 不可自审（40137）→ 校验被审批单据仍待审批（40104 / 40136）
/// → 同一事务内执行**生效动作**（各单据 <c>*Fulfillment</c>，与创建路径共用）→ 单据与记录状态置「已通过」
/// → 操作日志 → 提交 → 通知提交人。
/// **生效失败（如库存不足 40103）整体回滚**，审批保持「待审批」可重试或驳回（不产生半截数据）。
/// </summary>
public sealed class ApproveOrderRequestHandler : IRequestHandler<ApproveOrderRequest, ApprovalDetailDto>
{
    private readonly IApprovalRepository _approvalRepository;
    private readonly IPurchaseReceiptRepository _purchaseReceiptRepository;
    private readonly ISalesShipmentRepository _salesShipmentRepository;
    private readonly IPurchaseReturnRepository _purchaseReturnRepository;
    private readonly ISalesReturnRepository _salesReturnRepository;
    private readonly PurchaseReceiptFulfillment _purchaseReceiptFulfillment;
    private readonly SalesShipmentFulfillment _salesShipmentFulfillment;
    private readonly PurchaseReturnFulfillment _purchaseReturnFulfillment;
    private readonly SalesReturnFulfillment _salesReturnFulfillment;
    private readonly ApprovalDetailBuilder _detailBuilder;
    private readonly ApprovalNotifier _approvalNotifier;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化审批通过用例处理器
    /// </summary>
    public ApproveOrderRequestHandler(
        IApprovalRepository approvalRepository,
        IPurchaseReceiptRepository purchaseReceiptRepository,
        ISalesShipmentRepository salesShipmentRepository,
        IPurchaseReturnRepository purchaseReturnRepository,
        ISalesReturnRepository salesReturnRepository,
        PurchaseReceiptFulfillment purchaseReceiptFulfillment,
        SalesShipmentFulfillment salesShipmentFulfillment,
        PurchaseReturnFulfillment purchaseReturnFulfillment,
        SalesReturnFulfillment salesReturnFulfillment,
        ApprovalDetailBuilder detailBuilder,
        ApprovalNotifier approvalNotifier,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _approvalRepository = approvalRepository;
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _salesShipmentRepository = salesShipmentRepository;
        _purchaseReturnRepository = purchaseReturnRepository;
        _salesReturnRepository = salesReturnRepository;
        _purchaseReceiptFulfillment = purchaseReceiptFulfillment;
        _salesShipmentFulfillment = salesShipmentFulfillment;
        _purchaseReturnFulfillment = purchaseReturnFulfillment;
        _salesReturnFulfillment = salesReturnFulfillment;
        _detailBuilder = detailBuilder;
        _approvalNotifier = approvalNotifier;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理审批通过请求
    /// </summary>
    /// <param name="request">审批请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ApprovalDetailDto> HandleAsync(ApproveOrderRequest request, CancellationToken cancellationToken = default)
    {
        var approval = await _approvalRepository.GetByIdAsync(request.Id, cancellationToken);
        if (approval is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "审批记录不存在");
        }

        if (approval.Status != ApprovalStatus.Pending)
        {
            throw new BusinessException(ErrorCode.ApprovalStateInvalid, "该审批已处理，不能重复审批");
        }

        var operatorId = _currentUser.UserId()
            ?? throw new BusinessException(ErrorCode.Unauthorized, "登录状态无效，请重新登录");
        if (approval.SubmittedBy == operatorId)
        {
            // 不可自审：审批的意义在于第二双眼睛（042 §0.3）
            throw new BusinessException(ErrorCode.ApprovalSelfForbidden, "不能审批自己提交的单据");
        }

        var remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
        var typeText = AuditText.SettlementOrderType(approval.OrderType);
        var now = DateTimeOffset.UtcNow;

        // 生效与状态流转同一事务：任一环节失败（含库存不足 40103）整体回滚，审批保持待审批
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await ApplyAndMarkApprovedAsync(approval, operatorId, now, cancellationToken);
            await _approvalRepository.UpdateDecisionAsync(
                approval.Id, ApprovalStatus.Approved, operatorId, now, remark, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("status", "审批状态", AuditText.ApprovalStatus(ApprovalStatus.Pending), AuditText.ApprovalStatus(ApprovalStatus.Approved))
                .Add("decisionRemark", "审批意见", null, remark);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Approval,
                Action = AuditAction.Approve,
                ResourceId = approval.Id,
                ResourceNo = approval.OrderNo,
                Summary = $"审批通过{typeText} {approval.OrderNo}（往来：{approval.PartnerName}、{AuditSummary.Money(approval.Amount)}）{(remark is null ? string.Empty : $"｜意见：{remark}")}",
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

        // 提交之后：通知提交人（发信失败只记日志）+ 返回最新详情
        var decided = await _approvalRepository.GetByIdAsync(approval.Id, cancellationToken) ?? approval;
        await _approvalNotifier.NotifyDecidedAsync(decided, "通过", cancellationToken);
        return await _detailBuilder.BuildAsync(decided, cancellationToken);
    }

    /// <summary>执行被审批单据的生效动作并把其审批状态置为「已通过」</summary>
    private async Task ApplyAndMarkApprovedAsync(
        Approval approval, Guid operatorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        switch (approval.OrderType)
        {
            case SettlementOrderType.PurchaseInbound:
                {
                    var (order, items) = await _purchaseReceiptRepository.GetDetailAsync(approval.OrderId, cancellationToken: cancellationToken);
                    if (order is null)
                    {
                        throw new BusinessException(ErrorCode.NotFound, "被审批的采购入库单不存在");
                    }

                    EnsureApprovable(order.Status, order.ApprovalStatus, order.ReceiptNo, "采购入库单");
                    await _purchaseReceiptFulfillment.ApplyAsync(order, items, operatorId, now, cancellationToken);
                    await _purchaseReceiptRepository.UpdateApprovalStatusAsync(order.Id, ApprovalStatus.Approved, operatorId, cancellationToken);
                    return;
                }

            case SettlementOrderType.SalesOutbound:
                {
                    var (order, items) = await _salesShipmentRepository.GetDetailAsync(approval.OrderId, cancellationToken: cancellationToken);
                    if (order is null)
                    {
                        throw new BusinessException(ErrorCode.NotFound, "被审批的销售出库单不存在");
                    }

                    EnsureApprovable(order.Status, order.ApprovalStatus, order.ShipmentNo, "销售出库单");
                    await _salesShipmentFulfillment.ApplyAsync(order, items, operatorId, now, cancellationToken);
                    await _salesShipmentRepository.UpdateApprovalStatusAsync(order.Id, ApprovalStatus.Approved, operatorId, cancellationToken);
                    return;
                }

            case SettlementOrderType.PurchaseReturn:
                {
                    var (order, items) = await _purchaseReturnRepository.GetDetailAsync(approval.OrderId, cancellationToken: cancellationToken);
                    if (order is null)
                    {
                        throw new BusinessException(ErrorCode.NotFound, "被审批的采购退货单不存在");
                    }

                    EnsureApprovable(order.Status, order.ApprovalStatus, order.ReturnNo, "采购退货单");
                    await _purchaseReturnFulfillment.ApplyAsync(order, items, operatorId, now, cancellationToken);
                    await _purchaseReturnRepository.UpdateApprovalStatusAsync(order.Id, ApprovalStatus.Approved, operatorId, cancellationToken);
                    return;
                }

            case SettlementOrderType.SalesReturn:
                {
                    var (order, items) = await _salesReturnRepository.GetDetailAsync(approval.OrderId, cancellationToken: cancellationToken);
                    if (order is null)
                    {
                        throw new BusinessException(ErrorCode.NotFound, "被审批的销售退货单不存在");
                    }

                    EnsureApprovable(order.Status, order.ApprovalStatus, order.ReturnNo, "销售退货单");
                    await _salesReturnFulfillment.ApplyAsync(order, items, operatorId, now, cancellationToken);
                    await _salesReturnRepository.UpdateApprovalStatusAsync(order.Id, ApprovalStatus.Approved, operatorId, cancellationToken);
                    return;
                }

            default:
                throw new BusinessException(ErrorCode.Validation, "该单据类型不支持审批");
        }
    }

    /// <summary>单据必须「未作废 + 待审批」才允许审批通过（042 §0.2 / §3.5）</summary>
    private static void EnsureApprovable(OrderStatus status, ApprovalStatus approvalStatus, string orderNo, string typeText)
    {
        if (status == OrderStatus.Voided)
        {
            throw new BusinessException(ErrorCode.OrderVoided, $"{typeText} {orderNo} 已作废，不能审批通过");
        }

        if (approvalStatus != ApprovalStatus.Pending)
        {
            throw new BusinessException(ErrorCode.ApprovalStateInvalid, $"{typeText} {orderNo} 当前状态不允许审批通过");
        }
    }
}
