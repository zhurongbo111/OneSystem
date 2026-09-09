using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using App.Core.Dtos;
using App.Core.Errors;

namespace App.Core.Handlers;

/// <summary>
/// 用户 Handler 实现
/// </summary>
public class UserHandler : IUserHandler
{
    /// <summary>
    /// 从 JWT claims 还原当前用户
    /// </summary>
    public UserDto GetCurrentUser(ClaimsPrincipal principal)
    {
        var id = FirstValue(principal, ClaimTypes.NameIdentifier) ?? FirstValue(principal, JwtRegisteredClaimNames.Sub) ?? string.Empty;
        var username = FirstValue(principal, "username") ?? string.Empty;
        var displayName = FirstValue(principal, "displayName") ?? string.Empty;

        if (string.IsNullOrEmpty(username))
        {
            throw new BusinessException(ErrorCode.Unauthorized, "用户信息缺失，请重新登录");
        }

        return new UserDto { Id = id, Username = username, DisplayName = displayName };
    }

    /// <summary>
    /// 取指定类型的 claim 值（App.Core 不引用 ASP.NET Core，故不使用 FindFirstValue 扩展）
    /// </summary>
    private static string? FirstValue(ClaimsPrincipal principal, string claimType)
        => principal.FindFirst(claimType)?.Value;
}
