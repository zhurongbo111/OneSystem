namespace App.Core.Abstractions;

/// <summary>
/// 当前登录用户（由 App.Api 基于已认证的 ClaimsPrincipal 实现）。
/// 需要"当前用户"的 RequestHandler 依赖本接口，避免直接接触 HTTP 上下文。
/// </summary>
public interface ICurrentUser
{
    /// <summary>用户 ID</summary>
    string Id { get; }

    /// <summary>用户名；认证数据缺失时为空字符串</summary>
    string Username { get; }

    /// <summary>显示名称</summary>
    string DisplayName { get; }
}
