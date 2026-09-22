using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.AuditLogs.GetAuditLogs;

/// <summary>
/// 操作日志分页查询请求（Query 参数绑定；只读）。
/// 资源类型 / 动作用数值承载：非法值由校验器统一按 40000 拒绝，枚举名不作为对外契约。
/// </summary>
public sealed class GetAuditLogsRequest : IRequest<PagedResult<AuditLogListItemDto>>
{
    /// <summary>关键词（模糊匹配业务标识 / 操作人登录名 / 显示名），可空</summary>
    public string? Keyword { get; init; }

    /// <summary>资源类型筛选值（<c>AuditResource</c>），可空</summary>
    public int? Resource { get; init; }

    /// <summary>动作筛选值（<c>AuditAction</c>），可空</summary>
    public int? Action { get; init; }

    /// <summary>操作人 id 筛选，可空</summary>
    public Guid? UserId { get; init; }

    /// <summary>起始操作时间（含），可空</summary>
    public DateTimeOffset? Start { get; init; }

    /// <summary>结束操作时间（含），可空</summary>
    public DateTimeOffset? End { get; init; }

    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;
}
