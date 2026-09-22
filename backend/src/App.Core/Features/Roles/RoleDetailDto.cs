namespace App.Core.Features.Roles;

/// <summary>
/// 角色详情出参模型（新增 / 编辑 / 详情共用）
/// </summary>
public sealed class RoleDetailDto
{
    /// <summary>角色 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>角色名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>备注</summary>
    public string? Remark { get; init; }

    /// <summary>是否内置角色</summary>
    public bool IsBuiltin { get; init; }

    /// <summary>已勾选的权限点 key 集合</summary>
    public IReadOnlyList<string> PermissionKeys { get; init; } = [];

    /// <summary>绑定用户数</summary>
    public int UserCount { get; init; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
