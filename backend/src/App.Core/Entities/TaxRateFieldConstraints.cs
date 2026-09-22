namespace App.Core.Entities;

/// <summary>
/// 税率相关字段约束的**单一来源**：EF 实体配置（<c>HasMaxLength</c> / 精度 / 区间）与各 <c>RequestValidator</c>
/// 均引用本类常量，保证"格式校验"与"数据库约束"始终一致
/// （specs/031-erp-finance-master/design.md §2.3）。
/// 列表关键词按 <c>TaxRates.Name</c> 匹配，长度上限直接引用 <see cref="NameMaxLength"/>，不另立常量。
/// </summary>
public static class TaxRateFieldConstraints
{
    /// <summary>税率编码最大长度（对齐 TaxRates.Code varchar(20)）</summary>
    public const int CodeMaxLength = 20;

    /// <summary>税率名称最大长度（对齐 TaxRates.Name varchar(50)）</summary>
    public const int NameMaxLength = 50;

    /// <summary>备注最大长度（对齐 TaxRates.Remark varchar(200)）</summary>
    public const int RemarkMaxLength = 200;

    /// <summary>税率最小值（百分比）</summary>
    public const decimal RateMin = 0m;

    /// <summary>税率最大值（百分比）</summary>
    public const decimal RateMax = 100m;

    /// <summary>税率数值总位数（对齐 TaxRates.Rate numeric(9,4)）</summary>
    public const int RatePrecision = 9;

    /// <summary>税率数值小数位（对齐 TaxRates.Rate numeric(9,4)）</summary>
    public const int RateDecimalPlaces = 4;
}