namespace App.Core.Entities;

/// <summary>
/// 调拨单实体（对应 PostgreSQL 表 Transfers，specs/039-erp-transfer）。
/// 一步式单据：保存即生效（转出仓 −、转入仓 +，同一事务）；不支持编辑，只支持作废回冲。
/// 转出 / 转入仓名称、明细商品编码 / 名称 / 单位均为快照，后续档案修改不影响历史单据。
/// 调拨不落业务金额（无价格概念）；列表只展示「数量合计」与「明细行数」。
/// </summary>
public sealed class Transfer
{
    /// <summary>调拨单 ID</summary>
    public Guid Id { get; set; }

    /// <summary>单号，唯一，后端生成（TR + yyyyMMdd + 4 位序号，如 TR202609170001）</summary>
    public string TransferNo { get; set; } = string.Empty;

    /// <summary>转出仓 ID（外键 → Warehouses(Id)，038）</summary>
    public Guid FromWarehouseId { get; set; }

    /// <summary>转出仓名称快照（列表 / 详情 / 导出免 join；仓改名后历史单据保持当时名称）</summary>
    public string FromWarehouseName { get; set; } = string.Empty;

    /// <summary>转入仓 ID（外键 → Warehouses(Id)，038）</summary>
    public Guid ToWarehouseId { get; set; }

    /// <summary>转入仓名称快照（列表 / 详情 / 导出免 join）</summary>
    public string ToWarehouseName { get; set; } = string.Empty;

    /// <summary>业务日期（UTC 午夜）</summary>
    public DateTimeOffset TransferDate { get; set; }

    /// <summary>明细行数（列表展示，后端重算）</summary>
    public int ItemCount { get; set; }

    /// <summary>数量合计（列表展示，后端重算）</summary>
    public int TotalQuantity { get; set; }

    /// <summary>单据状态（1=正常 0=已作废；作废后禁止再操作）</summary>
    public OrderStatus Status { get; set; } = OrderStatus.Normal;

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>创建人用户 id</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>更新人用户 id</summary>
    public Guid? UpdatedBy { get; set; }
}
