using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 用户仓储接口（实现见 App.Infrastructure；首个业务功能由 EF Core 实现替换内存实现）。
/// Repository 只做数据访问，不做业务判定。
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// 按用户名查询用户，不存在返回 null
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
}
