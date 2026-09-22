namespace App.Core.Entities;

/// <summary>
/// 税率实体（对应 PostgreSQL 表 TaxRates）。
/// 税率字典供发票（`032`）与凭证（`033`）计量税额；<see cref="Rate"/> 为百分比数值（`13` 表示 13%）
/// （specs/031-erp-finance-master/design.md §2.2）。
/// </summary>
public sealed class TaxRate
{
    /// <summary>税率 ID</summary>
    public Guid Id { get; set; }

    /// <summary>税率编码，全局唯一（如 VAT13）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>税率名称，全局唯一（如「增值税 13%」）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>税率百分比数值（<c>13</c> 表示 13%）</summary>
    public decimal Rate { get; set; }

    /// <summary>税率状态（启用 / 停用）</summary>
    public TaxRateStatus Status { get; set; } = TaxRateStatus.Enabled;

    /// <summary>备注，可空</summary>
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