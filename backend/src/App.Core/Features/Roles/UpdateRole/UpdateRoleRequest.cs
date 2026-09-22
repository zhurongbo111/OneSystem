using App.Core.Abstractions;

namespace App.Core.Features.Roles.UpdateRole;

/// <summary>
/// 编辑角色请求（以路由 id 为准）；内置角色 <c>SuperAdmin</c> 不可编辑
/// </summary>
public sealed class UpdateRoleRequest : IRequest<RoleDetailDto>
{
    /// <summary>角色 id（以路由 id 为准）</summary>
    public Guid Id { get; init; }

    /// <summary>角色名称（Trim 后存储，大小写不敏感唯一，排除自身）</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>备注，可空；空串 / 纯空白按清空处理（PUT 全量覆盖语义）</summary>
    public string? Remark { get; init; }

    /// <summary>权限点 key 集合（全量替换，至少 1 项）</summary>
    public IReadOnlyList<string> PermissionKeys { get; init; } = [];
}
