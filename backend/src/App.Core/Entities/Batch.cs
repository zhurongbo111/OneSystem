namespace App.Core.Entities;

/// <summary>
/// 批次档案实体（对应 PostgreSQL 表 Batches，specs/040-erp-batch-expiry）。
/// 批次属于**商品**（<c>ProductId</c> + <c>BatchNo</c> 同商品内唯一），**跨仓共享**；
/// 批次号创建后不可修改；停用不删除，保留历史单据与流水引用。
/// 生产 / 到期日均为 UTC 午夜；到期日为空表示永不过期。
/// </summary>
public sealed class Batch
{
    /// <summary>批次 ID</summary>
    public Guid Id { get; set; }

    /// <summary>批次所属商品 ID（外键 → Products(Id)）</summary>
    public Guid ProductId { get; set; }

    /// <summary>批次号（同商品内唯一，大小写不敏感由应用层判定；创建后不可修改）</summary>
    public string BatchNo { get; set; } = string.Empty;

    /// <summary>生产日期（UTC 午夜，可空）</summary>
    public DateTimeOffset? ProductionDate { get; set; }

    /// <summary>到期日（UTC 午夜，可空；为空 = 永不过期）</summary>
    public DateTimeOffset? ExpiryDate { get; set; }

    /// <summary>批次状态（启用 / 停用；停用后不可再用于新的出入库单）</summary>
    public PartnerStatus Status { get; set; } = PartnerStatus.Enabled;

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
