namespace App.Core.Entities;

/// <summary>
/// 科目映射字段约束的**单一来源**：EF 实体配置（<c>HasMaxLength</c>）与各 <c>RequestValidator</c>
/// 均引用本类常量，禁止硬编码、禁止复制（specs/033-erp-general-ledger/design.md §2.5）。
/// </summary>
public static class AccountMappingFieldConstraints
{
    /// <summary>映射键最大长度（对齐 AccountMappings.Key varchar(30)）</summary>
    public const int KeyMaxLength = 30;
}
