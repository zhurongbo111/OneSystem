using App.Core.Entities;

namespace App.Core.Features.LoginLogs;

/// <summary>
/// 登录日志实体 → 出参模型 的映射（集中一处，避免各用例重复拼装）
/// </summary>
internal static class LoginLogDtoMapper
{
    /// <summary>映射列表项出参（登录名 / 显示名为登录时点快照，直接取自日志表）</summary>
    public static LoginLogListItemDto ToLoginLogListItemDto(UserLoginLog log)
        => new()
        {
            Id = log.Id.ToString(),
            UserId = log.UserId.ToString(),
            Username = log.Username,
            DisplayName = log.DisplayName,
            LoginAt = log.LoginAt,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
        };
}
