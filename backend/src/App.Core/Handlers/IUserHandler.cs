using System.Security.Claims;
using App.Core.Dtos;

namespace App.Core.Handlers;

/// <summary>
/// 用户 Handler：负责当前登录用户相关业务逻辑
/// </summary>
public interface IUserHandler
{
    /// <summary>
    /// 获取当前登录用户（从 JWT claims 还原）
    /// </summary>
    /// <param name="principal">已校验的 ClaimsPrincipal</param>
    UserDto GetCurrentUser(ClaimsPrincipal principal);
}
