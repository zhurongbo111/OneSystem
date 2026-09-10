using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 用户仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// Repository 只做数据访问，不做业务判定；一次写操作由方法自身持久化。
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// 按 id 查询用户，不存在返回 null
    /// </summary>
    /// <param name="id">用户 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按用户名查询用户（忽略大小写），不存在返回 null
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// 用户名是否已存在（忽略大小写）
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// 邮箱是否已被占用（忽略大小写）；编辑时可排除自身
    /// </summary>
    /// <param name="email">邮箱</param>
    /// <param name="excludeUserId">需要排除的用户 id（编辑场景传自身 id）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsByEmailAsync(string email, Guid? excludeUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 手机号是否已被占用；编辑时可排除自身
    /// </summary>
    /// <param name="phone">手机号</param>
    /// <param name="excludeUserId">需要排除的用户 id（编辑场景传自身 id）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsByPhoneAsync(string phone, Guid? excludeUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分页查询用户：按关键词（用户名 / 显示名模糊）+ 状态筛选，创建时间倒序
    /// </summary>
    /// <param name="keyword">关键词，可空</param>
    /// <param name="status">状态，可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前页数据与总条数</returns>
    Task<(IReadOnlyList<User> Items, int Total)> GetPagedAsync(
        string? keyword,
        UserStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增用户并持久化
    /// </summary>
    /// <param name="user">用户实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新用户并持久化
    /// </summary>
    /// <param name="user">用户实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// 仅更新最近登录时间（登录成功后调用，不触碰 UpdatedAt）
    /// </summary>
    /// <param name="id">用户 id</param>
    /// <param name="lastLoginAt">最近登录时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateLastLoginAsync(Guid id, DateTimeOffset lastLoginAt, CancellationToken cancellationToken = default);
}
