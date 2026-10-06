using App.Core.Entities;

namespace App.Core.Features.Approvals;

/// <summary>
/// 审批域实体 → 出参映射（specs/042-erp-approval/design.md §3.4）：
/// 正向映射集中在此，禁止在 Handler 内联拼装 DTO（后端规则 §4.3）。
/// </summary>
internal static class ApprovalsDtoMapper
{
    /// <summary>
    /// 审批记录 → 列表行 DTO
    /// </summary>
    /// <param name="approval">审批记录</param>
    /// <param name="submittedByName">提交人显示名</param>
    /// <param name="decidedByName">审批人显示名（未决定时为空）</param>
    public static ApprovalListItemDto ToApprovalListItemDto(
        Approval approval, string submittedByName, string? decidedByName)
        => new()
        {
            Id = approval.Id.ToString(),
            OrderType = (int)approval.OrderType,
            OrderId = approval.OrderId.ToString(),
            OrderNo = approval.OrderNo,
            PartnerName = approval.PartnerName,
            Amount = approval.Amount,
            Status = (int)approval.Status,
            SubmittedBy = approval.SubmittedBy.ToString(),
            SubmittedByName = submittedByName,
            SubmittedAt = approval.SubmittedAt,
            DecidedBy = approval.DecidedBy?.ToString(),
            DecidedByName = decidedByName,
            DecidedAt = approval.DecidedAt,
            DecisionRemark = approval.DecisionRemark,
        };

    /// <summary>
    /// 审批记录 + 被审批单据摘要与明细 → 详情 DTO
    /// </summary>
    /// <param name="approval">审批记录</param>
    /// <param name="submittedByName">提交人显示名</param>
    /// <param name="decidedByName">审批人显示名（未决定时为空）</param>
    /// <param name="orderDate">被审批单据业务日期</param>
    /// <param name="warehouseName">被审批单据仓库名称快照</param>
    /// <param name="items">被审批单据明细（只读）</param>
    public static ApprovalDetailDto ToApprovalDetailDto(
        Approval approval,
        string submittedByName,
        string? decidedByName,
        DateTimeOffset orderDate,
        string warehouseName,
        IReadOnlyList<ApprovalItemDto> items)
        => new()
        {
            Id = approval.Id.ToString(),
            OrderType = (int)approval.OrderType,
            OrderId = approval.OrderId.ToString(),
            OrderNo = approval.OrderNo,
            PartnerName = approval.PartnerName,
            Amount = approval.Amount,
            Status = (int)approval.Status,
            SubmittedBy = approval.SubmittedBy.ToString(),
            SubmittedByName = submittedByName,
            SubmittedAt = approval.SubmittedAt,
            DecidedBy = approval.DecidedBy?.ToString(),
            DecidedByName = decidedByName,
            DecidedAt = approval.DecidedAt,
            DecisionRemark = approval.DecisionRemark,
            OrderDate = orderDate,
            WarehouseName = warehouseName,
            Items = items,
        };

    /// <summary>采购入库单明细 → 审批明细行（四类单据明细同构，重载区分源类型）</summary>
    /// <param name="item">明细行实体</param>
    public static ApprovalItemDto ToApprovalItemDto(PurchaseReceiptItem item)
        => Build(item.ProductName, item.Unit, item.BatchNo, item.Quantity, item.UnitPrice, item.Subtotal);

    /// <summary>销售出库单明细 → 审批明细行</summary>
    /// <param name="item">明细行实体</param>
    public static ApprovalItemDto ToApprovalItemDto(SalesShipmentItem item)
        => Build(item.ProductName, item.Unit, item.BatchNo, item.Quantity, item.UnitPrice, item.Subtotal);

    /// <summary>采购退货单明细 → 审批明细行</summary>
    /// <param name="item">明细行实体</param>
    public static ApprovalItemDto ToApprovalItemDto(PurchaseReturnItem item)
        => Build(item.ProductName, item.Unit, item.BatchNo, item.Quantity, item.UnitPrice, item.Subtotal);

    /// <summary>销售退货单明细 → 审批明细行</summary>
    /// <param name="item">明细行实体</param>
    public static ApprovalItemDto ToApprovalItemDto(SalesReturnItem item)
        => Build(item.ProductName, item.Unit, item.BatchNo, item.Quantity, item.UnitPrice, item.Subtotal);

    /// <summary>
    /// 单据类型 + 规则实体 → 规则 DTO（规则缺失时返回默认「未启用 / 阈值 0」行）
    /// </summary>
    /// <param name="orderType">单据类型</param>
    /// <param name="rule">规则实体（可空）</param>
    public static ApprovalRuleDto ToApprovalRuleDto(SettlementOrderType orderType, ApprovalRule? rule)
        => new()
        {
            OrderType = (int)orderType,
            ThresholdAmount = rule?.ThresholdAmount ?? 0m,
            Enabled = rule?.Enabled ?? false,
        };

    private static ApprovalItemDto Build(
        string productName, string unit, string? batchNo, int quantity, decimal unitPrice, decimal subtotal)
        => new()
        {
            ProductName = productName,
            Unit = unit,
            BatchNo = batchNo,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Subtotal = subtotal,
        };
}
