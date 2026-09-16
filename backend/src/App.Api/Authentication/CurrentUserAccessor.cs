using System.Security.Claims;

using App.Core.Abstractions;

namespace App.Api.Authentication;

/// <summary>
/// 当前登录用户实现：从 HttpContext.User（已由 JWT 认证中间件写入）读取 claims。
/// 仅做 ClaimsPrincipal → 用户信息 的读取，不含业务逻辑。
/// </summary>
internal sealed class CurrentUserAccessor : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// 初始化当前用户访问器
    /// </summary>
    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>用户 ID（入站映射后通常落在 ClaimTypes.NameIdentifier）</summary>
    public string Id => FirstValue(ClaimTypes.NameIdentifier) ?? FirstValue("sub") ?? string.Empty;

    /// <summary>用户名；认证数据缺失时为空字符串</summary>
    public string Username => FirstValue("username") ?? string.Empty;

    /// <summary>显示名称</summary>
    public string DisplayName => FirstValue("displayName") ?? string.Empty;

    private string? FirstValue(string claimType)
        => _httpContextAccessor.HttpContext?.User.FindFirst(claimType)?.Value;
}
