using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 角色仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// Repository 只做数据访问，不做业务判定；一次写操作由方法自身持久化。
/// </summary>
public interface IRoleRepository
{
    /// <summary>
    /// 按 id 查询角色，不存在返回 null
    /// </summary>
    /// <param name="id">角色 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按名称集合查询角色（不保证顺序；用于校验角色 id 是否全部存在）
    /// </summary>
    /// <param name="ids">角色 id 集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<Role>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// 角色名称是否已存在（忽略大小写）；编辑时可排除自身
    /// </summary>
    /// <param name="name">角色名称</param>
    /// <param name="excludeRoleId">需要排除的角色 id（编辑场景传自身 id）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsByNameAsync(string name, Guid? excludeRoleId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分页查询角色：按名称模糊筛选，含权限数与用户数，创建时间正序
    /// </summary>
    /// <param name="keyword">关键词，可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前页数据与总条数</returns>
    Task<(IReadOnlyList<RoleListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增角色并持久化
    /// </summary>
    /// <param name="role">角色实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(Role role, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新角色并持久化
    /// </summary>
    /// <param name="role">角色实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(Role role, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除角色及其全部权限行并持久化
    /// </summary>
    /// <param name="role">角色实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteAsync(Role role, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询角色已勾选的权限点 key
    /// </summary>
    /// <param name="roleId">角色 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<string>> GetPermissionKeysAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 全量替换角色的权限点集合（先删后插）并持久化
    /// </summary>
    /// <param name="roleId">角色 id</param>
    /// <param name="permissionKeys">权限点 key 集合（已去重）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ReplacePermissionsAsync(Guid roleId, IReadOnlyCollection<string> permissionKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按名称查询角色（区分大小写无关的比较由应用层负责），不存在返回 null
    /// </summary>
    /// <param name="name">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}
