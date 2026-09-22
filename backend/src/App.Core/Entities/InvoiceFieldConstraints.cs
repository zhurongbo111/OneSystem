namespace App.Core.Entities;

/// <summary>
/// 发票字段约束的**单一来源**：EF 实体配置（<c>HasMaxLength</c> / 精度 / 索引）与各 <c>RequestValidator</c>
/// 均引用本类常量，禁止硬编码、禁止复制。
/// 备注长度引用 <see cref="OrderFieldConstraints.RemarkMaxLength"/>、明细行数上限引用
/// <see cref="OrderFieldConstraints.ItemsMaxCount"/>、金额上下界引用 <see cref="ProductFieldConstraints.PriceMinValue"/> /
/// <see cref="ProductFieldConstraints.PriceMaxValue"/>（同一规则同源，禁止另行定义）。
/// </summary>
public static class InvoiceFieldConstraints
{
    /// <summary>发票号码最大长度（对齐 Invoices.InvoiceNo varchar(50)）</summary>
    public const int InvoiceNoMaxLength = 50;

    /// <summary>列表查询关键词最大长度（对齐 InvoiceNo / PartnerName 的最大匹配列长，取 50）</summary>
    public const int KeywordMaxLength = 50;

    /// <summary>税率最小值（0 表示不计税，存小数口径如 0.13）</summary>
    public const decimal TaxRateMinValue = 0m;

    /// <summary>税率最大值（1 = 100%）</summary>
    public const decimal TaxRateMaxValue = 1m;

    /// <summary>税率数值总位数（对齐 Invoices.TaxRate numeric(5,4)）</summary>
    public const int TaxRatePrecision = 5;

    /// <summary>税率数值小数位（对齐 Invoices.TaxRate numeric(5,4)）</summary>
    public const int TaxRateDecimalPlaces = 4;

    /// <summary>金额数值总位数（对齐 Invoices / InvoiceItems 的 numeric(18,2) 金额列）</summary>
    public const int AmountPrecision = 18;

    /// <summary>金额数值小数位（对齐 Invoices / InvoiceItems 的 numeric(18,2) 金额列）</summary>
    public const int AmountDecimalPlaces = 2;
}