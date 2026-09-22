namespace App.Core.Entities;

/// <summary>
/// 审计日志字段约束的**单一来源**：EF 配置与查询 <c>RequestValidator</c> 均引用本类常量。
/// 用户相关长度同源引用 <see cref="UserFieldConstraints"/>，避免同一快照列长度分叉。
/// </summary>
public static class AuditLogFieldConstraints
{
    /// <summary>摘要最大长度（对齐 AuditLogs.Summary varchar(200)）</summary>
    public const int SummaryMaxLength = 200;

    /// <summary>业务标识最大长度（对齐 AuditLogs.ResourceNo varchar(50)；≥ Products.Code(32) / OrderNo(20) / Username(50) / 角色名(20)）</summary>
    public const int ResourceNoMaxLength = 50;

    /// <summary>操作人登录名最大长度（同源 <see cref="UserFieldConstraints.UsernameMaxLength"/>）</summary>
    public const int UsernameMaxLength = UserFieldConstraints.UsernameMaxLength;

    /// <summary>操作人显示名最大长度（同源 <see cref="UserFieldConstraints.DisplayNameMaxLength"/>）</summary>
    public const int DisplayNameMaxLength = UserFieldConstraints.DisplayNameMaxLength;

    /// <summary>操作人 / 业务标识关键词最大长度（按 ResourceNo / Username 列 Contains 匹配，超列长不可能命中）</summary>
    public const int KeywordMaxLength = ResourceNoMaxLength;

    /// <summary>字段级差异 JSON 文本上限；超出按尾部丢弃 + 摘要追加「（变更内容过长已截断）」</summary>
    public const int ChangesMaxLength = 8000;
}
