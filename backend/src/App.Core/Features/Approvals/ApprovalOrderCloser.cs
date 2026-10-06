using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Approvals;

/// <summary>
/// 被审批单据「关闭」组件（specs/042-erp-approval/design.md §0.2）：把待审批单据置为
/// 「指定审批状态（驳回 / 撤回）+ 单据作废」，驳回与撤回复用同一实现。
/// **不做任何库存 / 流水 / 成本操作**：待审批单据从未生效，不存在回冲；单据不会重新提交。
/// 不自行 Commit：事务由调用方 <see cref="IUnitOfWork"/> 控制。
/// </summary>
public sealed class ApprovalOrderCloser
{
    private readonly IPurchaseReceiptRepository _purchaseReceiptRepository;
    private readonly ISalesShipmentRepository _salesShipmentRepository;
    private readonly IPurchaseReturnRepository _purchaseReturnRepository;
    private readonly ISalesReturnRepository _salesReturnRepository;

    /// <summary>
    /// 初始化被审批单据关闭组件
    /// </summary>
    public ApprovalOrderCloser(
        IPurchaseReceiptRepository purchaseReceiptRepository,
        ISalesShipmentRepository salesShipmentRepository,
        IPurchaseReturnRepository purchaseReturnRepository,
        ISalesReturnRepository salesReturnRepository)
    {
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _salesShipmentRepository = salesShipmentRepository;
        _purchaseReturnRepository = purchaseReturnRepository;
        _salesReturnRepository = salesReturnRepository;
    }

    /// <summary>
    /// 关闭被审批单据（驳回 / 撤回）
    /// </summary>
    /// <param name="approval">审批记录（提供单据类型与单据 id）</param>
    /// <param name="targetStatus">目标审批状态（<see cref="ApprovalStatus.Rejected"/> / <see cref="ApprovalStatus.Withdrawn"/>）</param>
    /// <param name="operatorId">操作人 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task CloseAsync(
        Approval approval, ApprovalStatus targetStatus, Guid operatorId, CancellationToken cancellationToken = default)
    {
        switch (approval.OrderType)
        {
            case SettlementOrderType.PurchaseInbound:
                {
                    var (order, _) = await _purchaseReceiptRepository.GetDetailAsync(approval.OrderId, false, cancellationToken);
                    EnsurePending(order is not null, order?.Status, order?.ApprovalStatus, order?.ReceiptNo, "采购入库单");
                    await _purchaseReceiptRepository.UpdateApprovalStatusAsync(approval.OrderId, targetStatus, operatorId, cancellationToken);
                    await _purchaseReceiptRepository.UpdateStatusAsync(approval.OrderId, OrderStatus.Voided, operatorId, cancellationToken);
                    return;
                }

            case SettlementOrderType.SalesOutbound:
                {
                    var (order, _) = await _salesShipmentRepository.GetDetailAsync(approval.OrderId, false, cancellationToken);
                    EnsurePending(order is not null, order?.Status, order?.ApprovalStatus, order?.ShipmentNo, "销售出库单");
                    await _salesShipmentRepository.UpdateApprovalStatusAsync(approval.OrderId, targetStatus, operatorId, cancellationToken);
                    await _salesShipmentRepository.UpdateStatusAsync(approval.OrderId, OrderStatus.Voided, operatorId, cancellationToken);
                    return;
                }

            case SettlementOrderType.PurchaseReturn:
                {
                    var (order, _) = await _purchaseReturnRepository.GetDetailAsync(approval.OrderId, false, cancellationToken);
                    EnsurePending(order is not null, order?.Status, order?.ApprovalStatus, order?.ReturnNo, "采购退货单");
                    await _purchaseReturnRepository.UpdateApprovalStatusAsync(approval.OrderId, targetStatus, operatorId, cancellationToken);
                    await _purchaseReturnRepository.UpdateStatusAsync(approval.OrderId, OrderStatus.Voided, operatorId, cancellationToken);
                    return;
                }

            case SettlementOrderType.SalesReturn:
                {
                    var (order, _) = await _salesReturnRepository.GetDetailAsync(approval.OrderId, false, cancellationToken);
                    EnsurePending(order is not null, order?.Status, order?.ApprovalStatus, order?.ReturnNo, "销售退货单");
                    await _salesReturnRepository.UpdateApprovalStatusAsync(approval.OrderId, targetStatus, operatorId, cancellationToken);
                    await _salesReturnRepository.UpdateStatusAsync(approval.OrderId, OrderStatus.Voided, operatorId, cancellationToken);
                    return;
                }

            default:
                throw new BusinessException(ErrorCode.Validation, "该单据类型不支持审批");
        }
    }

    /// <summary>单据必须存在（40400）且「未作废 + 待审批」（40104 / 40136）才允许关闭</summary>
    private static void EnsurePending(
        bool exists, OrderStatus? status, ApprovalStatus? approvalStatus, string? orderNo, string typeText)
    {
        if (!exists || status is null || approvalStatus is null)
        {
            throw new BusinessException(ErrorCode.NotFound, $"被审批的{typeText}不存在");
        }

        if (status == OrderStatus.Voided)
        {
            throw new BusinessException(ErrorCode.OrderVoided, $"{typeText} {orderNo} 已作废，不能驳回或撤回");
        }

        if (approvalStatus != ApprovalStatus.Pending)
        {
            throw new BusinessException(ErrorCode.ApprovalStateInvalid, $"{typeText} {orderNo} 当前状态不允许驳回或撤回");
        }
    }
}
