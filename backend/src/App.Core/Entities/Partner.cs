namespace App.Core.Entities;

/// <summary>
/// 往来单位实体（对应 PostgreSQL 表 Partners，供应商 / 客户合并一张表）。
/// 名称唯一且创建后不可修改；只停用不删除，保留历史单据引用；
/// 采购单 / 销售单的类型校验在 erp-purchase / erp-sale 的 Handler 中执行。
/// </summary>
public sealed class Partner
{
    /// <summary>往来单位 ID</summary>
    public Guid Id { get; set; }

    /// <summary>单位名称，唯一，创建后不可修改</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>单位类型（供应商 / 客户 / 两者）</summary>
    public PartnerType Type { get; set; }

    /// <summary>联系人</summary>
    public string? Contact { get; set; }

    /// <summary>联系电话</summary>
    public string? Phone { get; set; }

    /// <summary>地址</summary>
    public string? Address { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>单位状态（启用 / 停用；停用不可被新单据选择）</summary>
    public PartnerStatus Status { get; set; } = PartnerStatus.Enabled;

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>创建人用户 id</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>更新人用户 id</summary>
    public Guid? UpdatedBy { get; set; }
}
