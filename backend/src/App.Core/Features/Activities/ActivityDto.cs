namespace App.Core.Features.Activities;

/// <summary>
/// 跟进活动出参共享模型（与前端 DTO camelCase 一一对应）。
/// 枚举回整数值，文案映射见 specs/043-erp-crm-presale design.md §0.1（前端映射）。
/// </summary>
public sealed class ActivityItemDto
{
    /// <summary>活动 id</summary>
    public required string Id { get; init; }

    /// <summary>归属业务类型（0 线索 / 1 商机）</summary>
    public required int BizType { get; init; }

    /// <summary>归属业务 id</summary>
    public required string BizId { get; init; }

    /// <summary>跟进方式（0 电话 / 1 拜访 / 2 邮件 / 3 其他）</summary>
    public required int Type { get; init; }

    /// <summary>跟进内容</summary>
    public required string Content { get; init; }

    /// <summary>跟进时间</summary>
    public required DateTimeOffset ActivityTime { get; init; }

    /// <summary>记录人显示名，取不到时为空</summary>
    public string? RecorderName { get; init; }

    /// <summary>创建时间（记录时间）</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
