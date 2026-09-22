namespace App.Core.Abstractions;

/// <summary>
/// 用户角色关联仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// 集合型从属表：更新一律全量替换，不做差异计算。
/// </summary>
public interface IUserRoleRepository
{
    /// <summary>
    /// 查询指定用户绑定的角色 id 集合
    /// </summary>
    /// <param name="userId">用户 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<Guid>> GetRoleIdsByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按用户 id 集合查询各自绑定的角色（一次取数避免 N+1）；缺失用户不出现在结果中
    /// </summary>
    /// <param name="userIds">用户 id 集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<UserRoleItem>>> GetRolesByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 全量替换用户的角色集合（先删后插）并持久化
    /// </summary>
    /// <param name="userId">用户 id</param>
    /// <param name="roleIds">角色 id 集合（已去重）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ReplaceUserRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 统计绑定指定角色的用户数（删除角色前判断是否被占用）
    /// </summary>
    /// <param name="roleId">角色 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<int> CountByRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量统计各角色绑定的用户数；未出现在字典中的角色视为 0
    /// </summary>
    /// <param name="roleIds">角色 id 集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyDictionary<Guid, int>> CountByRoleIdsAsync(
        IReadOnlyCollection<Guid> roleIds,
        CancellationToken cancellationToken = default);
}
