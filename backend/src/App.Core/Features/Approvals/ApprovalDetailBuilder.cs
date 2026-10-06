using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Approvals;

/// <summary>
/// 审批详情组装组件（specs/042-erp-approval/design.md §3.4）：按单据类型分派仓储读取被审批单据的
/// 摘要（业务日期 / 仓）与明细（只读展示），并解析提交人 / 审批人显示名，统一产出
/// <see cref="ApprovalDetailDto"/>。只读、无状态，不参与写流程（审批动作的生效由各单据
/// <c>*Fulfillment</c> 组件承担）。
/// </summary>
public sealed class ApprovalDetailBuilder
{
    private readonly IPurchaseReceiptRepository _purchaseReceiptRepository;
    private readonly ISalesShipmentRepository _salesShipmentRepository;
    private readonly IPurchaseReturnRepository _purchaseReturnRepository;
    private readonly ISalesReturnRepository _salesReturnRepository;
    private readonly IUserRepository _userRepository;

    /// <summary>
    /// 初始化审批详情组装组件
    /// </summary>
    public ApprovalDetailBuilder(
        IPurchaseReceiptRepository purchaseReceiptRepository,
        ISalesShipmentRepository salesShipmentRepository,
        IPurchaseReturnRepository purchaseReturnRepository,
        ISalesReturnRepository salesReturnRepository,
        IUserRepository userRepository)
    {
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _salesShipmentRepository = salesShipmentRepository;
        _purchaseReturnRepository = purchaseReturnRepository;
        _salesReturnRepository = salesReturnRepository;
        _userRepository = userRepository;
    }

    /// <summary>
    /// 组装审批详情（审批记录快照 + 被审批单据摘要与明细；单据不存在 → 40400）
    /// </summary>
    /// <param name="approval">审批记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ApprovalDetailDto> BuildAsync(Approval approval, CancellationToken cancellationToken = default)
    {
        var (orderDate, warehouseName, items) = await ReadOrderAsync(approval, cancellationToken);

        var userIds = new List<Guid> { approval.SubmittedBy };
        if (approval.DecidedBy is not null)
        {
            userIds.Add(approval.DecidedBy.Value);
        }

        var names = await _userRepository.GetDisplayNamesByIdsAsync(userIds, cancellationToken);

        return ApprovalsDtoMapper.ToApprovalDetailDto(
            approval,
            NameOf(names, approval.SubmittedBy),
            approval.DecidedBy is null ? null : NameOf(names, approval.DecidedBy.Value),
            orderDate,
            warehouseName,
            items);
    }

    /// <summary>按单据类型读取被审批单据摘要与明细</summary>
    private async Task<(DateTimeOffset OrderDate, string WarehouseName, IReadOnlyList<ApprovalItemDto> Items)> ReadOrderAsync(
        Approval approval, CancellationToken cancellationToken)
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

                    return (order.OrderDate, order.WarehouseName, items.Select(ApprovalsDtoMapper.ToApprovalItemDto).ToList());
                }

            case SettlementOrderType.SalesOutbound:
                {
                    var (order, items) = await _salesShipmentRepository.GetDetailAsync(approval.OrderId, cancellationToken: cancellationToken);
                    if (order is null)
                    {
                        throw new BusinessException(ErrorCode.NotFound, "被审批的销售出库单不存在");
                    }

                    return (order.OrderDate, order.WarehouseName, items.Select(ApprovalsDtoMapper.ToApprovalItemDto).ToList());
                }

            case SettlementOrderType.PurchaseReturn:
                {
                    var (order, items) = await _purchaseReturnRepository.GetDetailAsync(approval.OrderId, cancellationToken: cancellationToken);
                    if (order is null)
                    {
                        throw new BusinessException(ErrorCode.NotFound, "被审批的采购退货单不存在");
                    }

                    return (order.ReturnDate, order.WarehouseName, items.Select(ApprovalsDtoMapper.ToApprovalItemDto).ToList());
                }

            case SettlementOrderType.SalesReturn:
                {
                    var (order, items) = await _salesReturnRepository.GetDetailAsync(approval.OrderId, cancellationToken: cancellationToken);
                    if (order is null)
                    {
                        throw new BusinessException(ErrorCode.NotFound, "被审批的销售退货单不存在");
                    }

                    return (order.ReturnDate, order.WarehouseName, items.Select(ApprovalsDtoMapper.ToApprovalItemDto).ToList());
                }

            default:
                // 盘点单 / 调拨单无业务金额、不纳入审批（042 §5 范围外）
                throw new BusinessException(ErrorCode.Validation, "该单据类型不支持审批");
        }
    }

    private static string NameOf(IReadOnlyDictionary<Guid, string> names, Guid userId)
        => names.TryGetValue(userId, out var name) ? name : string.Empty;
}
