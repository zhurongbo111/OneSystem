using App.Core.Entities;

namespace App.Core.Audit;

/// <summary>
/// 审计日志写入模型：由各写用例在业务写完、**提交事务之前**构造并经 <c>IAuditLogger</c> 落库。
/// 操作人（id / 登录名 / 显示名快照）由 <c>IAuditLogger</c> 实现经 <c>ICurrentUser</c> 填充，调用方不感知。
/// </summary>
public sealed class AuditEntry
{
    /// <summary>资源类型</summary>
    public AuditResource Resource { get; init; }

    /// <summary>动作</summary>
    public AuditAction Action { get; init; }

    /// <summary>业务对象 id（如单据 id / 商品 id）；无明确对象时可空</summary>
    public Guid? ResourceId { get; init; }

    /// <summary>业务标识快照（单号 / 编码 / 用户名 / 角色名）</summary>
    public string? ResourceNo { get; init; }

    /// <summary>中文摘要（单行、含业务标识，便于人读）</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>字段级差异 JSON 文本（由 <see cref="AuditChangeBuilder"/> 产出）；无差异时为 <c>null</c></summary>
    public string? Changes { get; init; }

    /// <summary>差异内容是否因超长被截断；为真时实现方会在摘要尾部追加截断标注</summary>
    public bool ChangesTruncated { get; init; }

    /// <summary>操作时间（UTC）；由调用方传入用例既有的时间戳，保持单测可注入</summary>
    public DateTimeOffset UtcNow { get; init; }
}
