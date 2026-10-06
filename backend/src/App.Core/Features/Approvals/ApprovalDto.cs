namespace App.Core.Features.Approvals;

/// <summary>
/// 审批记录列表行出参（specs/042-erp-approval/design.md §3.4；提交人 / 审批人由 Handler 批量解析显示名）
/// </summary>
public sealed class ApprovalListItemDto
{
    /// <summary>审批记录 id</summary>
    public required string Id { get; init; }

    /// <summary>单据类型（0 采购入库 / 1 销售出库 / 2 采购退货 / 3 销售退货）</summary>
    public required int OrderType { get; init; }

    /// <summary>被审批单据 id</summary>
    public required string OrderId { get; init; }

    /// <summary>单据号快照</summary>
    public required string OrderNo { get; init; }

    /// <summary>往来单位名称快照</summary>
    public required string PartnerName { get; init; }

    /// <summary>单据金额快照</summary>
    public required decimal Amount { get; init; }

    /// <summary>审批状态（1 待审批 / 2 已通过 / 3 已驳回 / 4 已撤回）</summary>
    public required int Status { get; init; }

    /// <summary>提交人 id</summary>
    public required string SubmittedBy { get; init; }

    /// <summary>提交人显示名（查不到时回落用户名 / 空串）</summary>
    public required string SubmittedByName { get; init; }

    /// <summary>提交时间</summary>
    public required DateTimeOffset SubmittedAt { get; init; }

    /// <summary>审批人 id（未决定时为空）</summary>
    public string? DecidedBy { get; init; }

    /// <summary>审批人显示名（未决定时为空）</summary>
    public string? DecidedByName { get; init; }

    /// <summary>审批时间（未决定时为空）</summary>
    public DateTimeOffset? DecidedAt { get; init; }

    /// <summary>审批意见（驳回必填、通过可空）</summary>
    public string? DecisionRemark { get; init; }
}

/// <summary>
/// 被审批单据明细行出参（只读展示；四类单据明细同构，取名称 / 单位 / 批次 / 数量 / 单价 / 小计）
/// </summary>
public sealed class ApprovalItemDto
{
    /// <summary>商品名称快照</summary>
    public required string ProductName { get; init; }

    /// <summary>计量单位快照</summary>
    public required string Unit { get; init; }

    /// <summary>批次号快照（040；非批次商品为空）</summary>
    public string? BatchNo { get; init; }

    /// <summary>数量</summary>
    public required int Quantity { get; init; }

    /// <summary>单价快照</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>小计（后端重算值）</summary>
    public required decimal Subtotal { get; init; }
}

/// <summary>
/// 审批记录详情出参（审批记录快照 + 被审批单据摘要与明细，specs/042-erp-approval/design.md §3.4）
/// </summary>
public sealed class ApprovalDetailDto
{
    /// <summary>审批记录 id</summary>
    public required string Id { get; init; }

    /// <summary>单据类型（0 采购入库 / 1 销售出库 / 2 采购退货 / 3 销售退货）</summary>
    public required int OrderType { get; init; }

    /// <summary>被审批单据 id</summary>
    public required string OrderId { get; init; }

    /// <summary>单据号快照</summary>
    public required string OrderNo { get; init; }

    /// <summary>往来单位名称快照</summary>
    public required string PartnerName { get; init; }

    /// <summary>单据金额快照</summary>
    public required decimal Amount { get; init; }

    /// <summary>审批状态（1 待审批 / 2 已通过 / 3 已驳回 / 4 已撤回）</summary>
    public required int Status { get; init; }

    /// <summary>提交人 id</summary>
    public required string SubmittedBy { get; init; }

    /// <summary>提交人显示名</summary>
    public required string SubmittedByName { get; init; }

    /// <summary>提交时间</summary>
    public required DateTimeOffset SubmittedAt { get; init; }

    /// <summary>审批人 id（未决定时为空）</summary>
    public string? DecidedBy { get; init; }

    /// <summary>审批人显示名（未决定时为空）</summary>
    public string? DecidedByName { get; init; }

    /// <summary>审批时间（未决定时为空）</summary>
    public DateTimeOffset? DecidedAt { get; init; }

    /// <summary>审批意见（驳回必填、通过可空）</summary>
    public string? DecisionRemark { get; init; }

    /// <summary>被审批单据的业务日期（审批抽屉摘要项）</summary>
    public required DateTimeOffset OrderDate { get; init; }

    /// <summary>被审批单据的仓库名称快照（审批抽屉摘要项）</summary>
    public required string WarehouseName { get; init; }

    /// <summary>被审批单据明细（只读，按插入顺序）</summary>
    public required IReadOnlyList<ApprovalItemDto> Items { get; init; }
}

/// <summary>
/// 审批规则出参（按单据类型一条；未配置的类型返回默认「未启用」行）
/// </summary>
public sealed class ApprovalRuleDto
{
    /// <summary>单据类型（0 采购入库 / 1 销售出库 / 2 采购退货 / 3 销售退货）</summary>
    public required int OrderType { get; init; }

    /// <summary>审批阈值（未配置时为 0）</summary>
    public required decimal ThresholdAmount { get; init; }

    /// <summary>是否启用（未配置时为 false = 该类单据保存即生效）</summary>
    public required bool Enabled { get; init; }
}
