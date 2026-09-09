using App.Core.Dtos;

namespace App.Core.Services;

/// <summary>
/// 用户账号服务：提供账号校验能力。
/// 注意：脚手架阶段为内存示例实现，由首个业务功能替换为基于 Repository 的实现。
/// </summary>
public interface IUserAccountService
{
    /// <summary>
    /// 校验用户名与密码，成功返回用户信息，失败返回 null
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    Task<UserDto?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
}
