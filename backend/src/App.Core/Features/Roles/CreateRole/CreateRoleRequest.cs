using App.Core.Abstractions;

namespace App.Core.Features.Roles.CreateRole;

/// <summary>
/// 新增角色请求：新增的角色一律为非内置角色（内置角色由种子创建）
/// </summary>
public sealed class CreateRoleRequest : IRequest<RoleDetailDto>
{
    /// <summary>角色名称（Trim 后存储，大小写不敏感唯一）</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>备注，可空；空串 / 纯空白按清空处理</summary>
    public string? Remark { get; init; }

    /// <summary>权限点 key 集合（全量覆盖，至少 1 项）</summary>
    public IReadOnlyList<string> PermissionKeys { get; init; } = [];
}
