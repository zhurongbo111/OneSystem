using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 角色仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// </summary>
public sealed class RoleRepository : IRoleRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化角色仓储
    /// </summary>
    public RoleRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        // 该查询同时服务于详情读取与编辑 / 删除，需跟踪实体以便后续更新，故不使用 AsNoTracking
        => _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Role>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await _dbContext.Roles.AsNoTracking()
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var lower = name.Trim().ToLowerInvariant();
        return _dbContext.Roles.FirstOrDefaultAsync(r => r.Name.ToLower() == lower, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> ExistsByNameAsync(string name, Guid? excludeRoleId = null, CancellationToken cancellationToken = default)
    {
        var lower = name.Trim().ToLowerInvariant();
        var query = _dbContext.Roles.AsNoTracking().Where(r => r.Name.ToLower() == lower);
        if (excludeRoleId is not null)
        {
            var excludeId = excludeRoleId.Value;
            query = query.Where(r => r.Id != excludeId);
        }

        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<RoleListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Roles.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(r => r.Name.ToLower().Contains(lower)
                || (r.Remark != null && r.Remark.ToLower().Contains(lower)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RoleListItem
            {
                Id = r.Id,
                Name = r.Name,
                Remark = r.Remark,
                IsBuiltin = r.IsBuiltin,
                CreatedAt = r.CreatedAt,
                PermissionCount = _dbContext.RolePermissions.Count(rp => rp.RoleId == r.Id),
                UserCount = _dbContext.UserRoles.Count(ur => ur.RoleId == r.Id),
            })
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public async Task AddAsync(Role role, CancellationToken cancellationToken = default)
    {
        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Role role, CancellationToken cancellationToken = default)
    {
        _dbContext.Roles.Update(role);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Role role, CancellationToken cancellationToken = default)
    {
        // 权限行随角色级联删除，此处显式清理由仓储保证（避免不同提供程序级联行为差异）
        await RemovePermissionsAsync(role.Id, cancellationToken);
        _dbContext.Roles.Remove(role);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetPermissionKeysAsync(Guid roleId, CancellationToken cancellationToken = default)
        => await _dbContext.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionKey)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task ReplacePermissionsAsync(
        Guid roleId,
        IReadOnlyCollection<string> permissionKeys,
        CancellationToken cancellationToken = default)
    {
        await RemovePermissionsAsync(roleId, cancellationToken);

        var rows = permissionKeys.Select(key => new RolePermission { RoleId = roleId, PermissionKey = key }).ToList();
        if (rows.Count > 0)
        {
            _dbContext.RolePermissions.AddRange(rows);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RemovePermissionsAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            _dbContext.RolePermissions.RemoveRange(existing);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
