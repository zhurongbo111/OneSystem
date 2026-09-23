namespace App.Core.Entities;

/// <summary>
/// 仓库实体（对应 PostgreSQL 表 Warehouses，specs/038-erp-multi-warehouse/design.md §2.1）。
/// 业务档案：编码唯一且创建后不可修改，名称唯一；全系统有且仅有一个默认仓（应用层保证）；
/// 只停用不删除，保留库存 / 流水 / 单据引用。
/// </summary>
public sealed class Warehouse
{
    /// <summary>仓库 ID</summary>
    public Guid Id { get; set; }

    /// <summary>仓库编码，唯一，创建后不可修改</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>仓库名称，唯一（大小写不敏感，可改）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>地址</summary>
    public string? Address { get; set; }

    /// <summary>联系人</summary>
    public string? Contact { get; set; }

    /// <summary>联系电话</summary>
    public string? Phone { get; set; }

    /// <summary>是否默认仓（全系统唯一；不可停用、不可删除，兜底「不传仓库」的开单）</summary>
    public bool IsDefault { get; set; }

    /// <summary>仓库状态（启用 / 停用；停用仓不可被新单据选择）</summary>
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
