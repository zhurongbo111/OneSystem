namespace App.Core.Entities;

/// <summary>
/// 跟进活动实体（对应 PostgreSQL 表 Activities）。
/// **纯追加表**：只增不改不删（跟进留痕，specs/043-erp-crm-presale design.md §0.1 / §5），
/// 故只有 <see cref="CreatedAt"/> / <see cref="CreatedBy"/> 审计字段，无 UpdatedAt / UpdatedBy。
/// 归属由 <see cref="BizType"/> + <see cref="BizId"/> 组合定位（线索或商机）。
/// </summary>
public sealed class Activity
{
    /// <summary>活动 ID</summary>
    public Guid Id { get; set; }

    /// <summary>归属业务类型（线索 / 商机）</summary>
    public ActivityBizType BizType { get; set; }

    /// <summary>归属业务 id（线索 id 或商机 id）</summary>
    public Guid BizId { get; set; }

    /// <summary>跟进方式（电话 / 拜访 / 邮件 / 其他）</summary>
    public ActivityType Type { get; set; }

    /// <summary>跟进内容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>跟进时间</summary>
    public DateTimeOffset ActivityTime { get; set; }

    /// <summary>创建时间（记录时间）</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>创建人用户 id（即记录人）</summary>
    public Guid? CreatedBy { get; set; }
}
