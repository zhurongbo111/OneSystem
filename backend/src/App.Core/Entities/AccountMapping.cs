namespace App.Core.Entities;

/// <summary>
/// 科目映射实体（对应 PostgreSQL 表 AccountMappings，specs/033-erp-general-ledger/design.md §2.4）。
/// 「业务事件 → 会计科目」的映射键唯一；自动凭证按映射取科目，缺失映射拒绝生成（40158）。
/// </summary>
public sealed class AccountMapping
{
    /// <summary>映射 ID</summary>
    public Guid Id { get; set; }

    /// <summary>映射键，唯一（Inventory / Receivable / Payable / Revenue / Cost / Cash / Bank / Profit）</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>目标科目 id（外键 → Accounts(Id)；须为末级启用科目）</summary>
    public Guid AccountId { get; set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>创建人用户 id</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>更新人用户 id</summary>
    public Guid? UpdatedBy { get; set; }
}
