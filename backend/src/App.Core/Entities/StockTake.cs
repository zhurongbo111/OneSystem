namespace App.Core.Entities;

/// <summary>
/// 盘点 / 期初建账单据实体（对应 PostgreSQL 表 StockTakes）。
/// 一步式单据：保存即生效（库存按实盘数量设定 + 差异行写流水）；不支持编辑 / 作废，纠错靠再盘。
/// 无状态字段：落库即在效（决策见 design.md §5）。
/// </summary>
public sealed class StockTake
{
    /// <summary>盘点单 ID</summary>
    public Guid Id { get; set; }

    /// <summary>单号，唯一，后端生成（ST + yyyyMMdd + 4 位序号，如 ST202609160001）</summary>
    public string TakeNo { get; set; } = string.Empty;

    /// <summary>单据类型（0=期初建账 1=库存盘点；流水类型与可选商品范围随类型不同）</summary>
    public StockTakeType Type { get; set; }

    /// <summary>盘点业务日期（UTC 午夜）</summary>
    public DateTimeOffset TakeDate { get; set; }

    /// <summary>明细行数（提交时统计）</summary>
    public int ItemCount { get; set; }

    /// <summary>差异行数（Difference != 0，提交时统计）</summary>
    public int DiffItemCount { get; set; }

    /// <summary>备注（盘点说明 / 差异原因）</summary>
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
