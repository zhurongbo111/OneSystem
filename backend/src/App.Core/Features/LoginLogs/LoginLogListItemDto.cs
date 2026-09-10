namespace App.Core.Features.LoginLogs;

/// <summary>
/// 登录日志列表出参模型（登录名 / 显示名为登录时点快照，直接取自日志表，无需 join 用户表）
/// </summary>
public sealed class LoginLogListItemDto
{
    /// <summary>日志 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>登录用户 id</summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>登录名（快照）</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>显示名称（快照）</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>登录时间</summary>
    public DateTimeOffset LoginAt { get; init; }

    /// <summary>客户端 IP</summary>
    public string? IpAddress { get; init; }

    /// <summary>客户端 User-Agent</summary>
    public string? UserAgent { get; init; }
}
