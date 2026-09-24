namespace App.Core.Entities;

/// <summary>
/// 调拨单明细实体（对应 PostgreSQL 表 TransferItems，specs/039-erp-transfer）。
/// 编码 / 名称 / 单位均为商品当时档案的**快照**；调拨无价格，明细不存成本（成本已落在流水，避免第二口径）。
/// 不软删除，作废单保留明细供审计。
/// </summary>
public sealed class TransferItem
{
    /// <summary>明细行 ID</summary>
    public Guid Id { get; set; }

    /// <summary>调拨单 ID（外键 → Transfers(Id)，索引）</summary>
    public Guid TransferId { get; set; }

    /// <summary>商品 ID（外键 → Products(Id)）</summary>
    public Guid ProductId { get; set; }

    /// <summary>商品编码快照</summary>
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>商品名称快照</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>计量单位快照</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>调拨数量（≥ 1）</summary>
    public int Quantity { get; set; }

    /// <summary>批次 ID（040-erp-batch 落地时启用；040 前恒为空）</summary>
    public Guid? BatchId { get; set; }
}
