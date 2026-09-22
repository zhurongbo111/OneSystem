namespace App.Core.Entities;

/// <summary>
/// 会计科目实体（对应 PostgreSQL 表 Accounts）。
/// 科目树用 <see cref="ParentId"/> 自引用维护（<c>NULL</c> = 一级科目）；防环在 Handler 内沿父链上溯判定；
/// 「末级科目」（可记账）为派生规则（无子科目即末级），不落列
/// （specs/031-erp-finance-master/design.md §2.1）。
/// </summary>
public sealed class Account
{
    /// <summary>科目 ID</summary>
    public Guid Id { get; set; }

    /// <summary>科目编码，全局唯一（如 1001 / 100101）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>科目名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>科目类别（资产 / 负债 / 权益 / 成本 / 损益）</summary>
    public AccountCategory Category { get; set; }

    /// <summary>余额方向（借 / 贷）</summary>
    public AccountDirection Direction { get; set; }

    /// <summary>上级科目 id（<c>null</c> 表示一级科目）</summary>
    public Guid? ParentId { get; set; }

    /// <summary>同级排序（升序展示）</summary>
    public int SortOrder { get; set; }

    /// <summary>预置科目（系统种子写入，不可删除，可改名 / 停用）</summary>
    public bool IsPreset { get; set; }

    /// <summary>科目状态（启用 / 停用）</summary>
    public AccountStatus Status { get; set; } = AccountStatus.Enabled;

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