namespace App.Core.Entities;

/// <summary>
/// 记账凭证相关字段约束的**单一来源**：EF 实体配置（<c>HasMaxLength</c> / 精度）与各 <c>RequestValidator</c>
/// 均引用本类常量，禁止硬编码、禁止复制（specs/033-erp-general-ledger/design.md §2.5）。
/// 分录行数上限引用 <see cref="OrderFieldConstraints.ItemsMaxCount"/>（同一规则同源）。
/// </summary>
public static class VoucherFieldConstraints
{
    /// <summary>凭证号最大长度（对齐 Vouchers.VoucherNo varchar(30)）</summary>
    public const int NoMaxLength = 30;

    /// <summary>凭证号前缀（凭证号形如「记-YYYYMM-0001」）</summary>
    public const string NoPrefix = "记-";

    /// <summary>凭证摘要最大长度（对齐 Vouchers.Summary varchar(200)）</summary>
    public const int SummaryMaxLength = 200;

    /// <summary>分录行摘要最大长度（对齐 VoucherEntries.Summary varchar(200)）</summary>
    public const int EntrySummaryMaxLength = 200;

    /// <summary>单张凭证分录行数上限</summary>
    public const int EntriesMaxCount = 200;

    /// <summary>单笔分录金额上界（对齐 numeric(18,2) 的最大值）</summary>
    public const decimal PerEntryMaxAmount = 9999999999999999.99m;

    /// <summary>单笔分录金额下界</summary>
    public const decimal PerEntryMinAmount = 0m;

    /// <summary>金额数值总位数（对齐 Vouchers / VoucherEntries 的 numeric(18,2) 金额列）</summary>
    public const int AmountPrecision = 18;

    /// <summary>金额数值小数位（对齐 Vouchers / VoucherEntries 的 numeric(18,2) 金额列）</summary>
    public const int AmountDecimalPlaces = 2;

    /// <summary>列表查询关键词最大长度（对齐凭证号 / 摘要的最大匹配列长，取 30）</summary>
    public const int KeywordMaxLength = 30;
}
