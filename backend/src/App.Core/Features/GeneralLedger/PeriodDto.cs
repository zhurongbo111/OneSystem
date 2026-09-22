namespace App.Core.Features.GeneralLedger;

/// <summary>
/// 会计期间出参模型（枚举以整型输出：状态 0 未结账 / 1 已结账）
/// </summary>
public sealed class PeriodDto
{
    /// <summary>期间 id</summary>
    public required string Id { get; init; }

    /// <summary>年</summary>
    public required int Year { get; init; }

    /// <summary>月（1–12）</summary>
    public required int Month { get; init; }

    /// <summary>期间状态（0 未结账 / 1 已结账）</summary>
    public required int Status { get; init; }

    /// <summary>结账时间，未结账为空</summary>
    public DateTimeOffset? ClosedAt { get; init; }

    /// <summary>结账人用户 id，未结账为空</summary>
    public string? ClosedBy { get; init; }
}
