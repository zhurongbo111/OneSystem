using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 内存用户仓储（脚手架临时实现；首个业务功能接入真实用户表后替换为 EF Core 实现）。
/// 种子账号：admin / admin123，displayName = 管理员。
/// </summary>
public sealed class InMemoryUserRepository : IUserRepository
{
    private static readonly User[] SeedUsers =
    [
        new() { Id = "1", Username = "admin", Password = "admin123", DisplayName = "管理员" },
    ];

    /// <summary>
    /// 按用户名查询用户（忽略大小写），不存在返回 null
    /// </summary>
    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        => Task.FromResult(SeedUsers.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)));
}
