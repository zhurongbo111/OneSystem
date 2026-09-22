using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Roles;

/// <summary>
/// 角色实体 / 读模型 → 出参模型 的映射（集中一处，避免各用例重复拼装）
/// </summary>
internal static class RoleDtoMapper
{
    /// <summary>映射列表项出参（读模型已含权限数与用户数）</summary>
    public static RoleListItemDto ToRoleListItemDto(RoleListItem item) => new()
    {
        Id = item.Id.ToString(),
        Name = item.Name,
        Remark = item.Remark,
        IsBuiltin = item.IsBuiltin,
        PermissionCount = item.PermissionCount,
        UserCount = item.UserCount,
        CreatedAt = item.CreatedAt,
    };

    /// <summary>
    /// 映射详情出参：权限点集合与用户数需联查，由用例传入后合并
    /// </summary>
    /// <param name="role">角色实体</param>
    /// <param name="permissionKeys">角色已勾选的权限点 key</param>
    /// <param name="userCount">绑定该角色的用户数</param>
    public static RoleDetailDto ToRoleDetailDto(Role role, IReadOnlyList<string> permissionKeys, int userCount) => new()
    {
        Id = role.Id.ToString(),
        Name = role.Name,
        Remark = role.Remark,
        IsBuiltin = role.IsBuiltin,
        PermissionKeys = permissionKeys,
        UserCount = userCount,
        CreatedAt = role.CreatedAt,
        UpdatedAt = role.UpdatedAt,
    };
}
