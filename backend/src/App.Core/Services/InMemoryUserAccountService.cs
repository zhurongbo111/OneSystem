using App.Core.Dtos;

namespace App.Core.Services;

/// <summary>
/// 内存示例用户账号服务（脚手架临时实现，首个业务功能接入真实用户表后替换）。
/// 测试账号：admin / admin123。
/// </summary>
public class InMemoryUserAccountService : IUserAccountService
{
    /// <summary>
    /// 校验用户名与密码
    /// </summary>
    public Task<UserDto?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase) && password == "admin123")
        {
            return Task.FromResult<UserDto?>(new UserDto { Id = "1", Username = "admin", DisplayName = "管理员" });
        }
        return Task.FromResult<UserDto?>(null);
    }
}
