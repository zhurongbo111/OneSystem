namespace App.Core.Entities;

/// <summary>
/// 登录日志实体（对应 PostgreSQL 表 UserLoginLogs）。
/// 本期只记录**成功**登录：只追加、不更新、不删除，作为登录审计轨迹。
/// 登录名 / 显示名为登录时点的快照，保证日志自包含。
/// </summary>
public sealed class UserLoginLog
{
    /// <summary>日志 ID</summary>
    public Guid Id { get; set; }

    /// <summary>登录用户 id</summary>
    public Guid UserId { get; set; }

    /// <summary>登录名（快照）</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>显示名称（快照）</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>登录时间</summary>
    public DateTimeOffset LoginAt { get; set; }

    /// <summary>客户端 IP；取不到时为空</summary>
    public string? IpAddress { get; set; }

    /// <summary>客户端 User-Agent；超长截断，取不到时为空</summary>
    public string? UserAgent { get; set; }
}
