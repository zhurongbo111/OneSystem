namespace App.Core.Entities;

/// <summary>
/// 业务操作审计日志实体（对应 PostgreSQL 表 AuditLogs）。
/// 纯追加表：无软删除、无 <c>UpdatedAt</c> / <c>UpdatedBy</c>，不提供更新 / 删除接口。
/// 操作人姓名与业务标识为操作时点的**快照**，保证日志自包含（用户可被停用、对象可被删除）。
/// </summary>
public sealed class AuditLog
{
    /// <summary>日志 ID</summary>
    public Guid Id { get; set; }

    /// <summary>操作人 id；无 HTTP 上下文（如系统动作）时为空</summary>
    public Guid? UserId { get; set; }

    /// <summary>操作人登录名快照</summary>
    public string? Username { get; set; }

    /// <summary>操作人显示名快照</summary>
    public string? DisplayName { get; set; }

    /// <summary>资源类型</summary>
    public AuditResource Resource { get; set; }

    /// <summary>动作</summary>
    public AuditAction Action { get; set; }

    /// <summary>业务对象 id（如单据 id / 商品 id）；跨多表不建外键</summary>
    public Guid? ResourceId { get; set; }

    /// <summary>业务标识快照（单号 / 编码 / 用户名 / 角色名）</summary>
    public string? ResourceNo { get; set; }

    /// <summary>中文摘要（单行，含业务标识，便于人读）</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>字段级差异 JSON 数组文本（<c>[{ field, label, before, after }]</c>）；无差异时为空</summary>
    public string? Changes { get; set; }

    /// <summary>操作时间（UTC）</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
