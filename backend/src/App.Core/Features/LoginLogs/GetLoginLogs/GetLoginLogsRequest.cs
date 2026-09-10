using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.LoginLogs.GetLoginLogs;

/// <summary>
/// 登录日志分页查询请求（Query 参数绑定；只读，无结果字段——本期只记录成功登录）
/// </summary>
public sealed class GetLoginLogsRequest : IRequest<PagedResult<LoginLogListItemDto>>
{
    /// <summary>登录名关键词（模糊匹配），可空</summary>
    public string? Username { get; init; }

    /// <summary>起始登录时间（含），可空</summary>
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>结束登录时间（含），可空</summary>
    public DateTimeOffset? EndTime { get; init; }

    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;
}
