namespace App.Core.Features.Roles;

/// <summary>
/// 角色列表出参模型（列表不需要全部字段，与详情模型分开）
/// </summary>
public sealed class RoleListItemDto
{
    /// <summary>角色 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>角色名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>备注</summary>
    public string? Remark { get; init; }

    /// <summary>是否内置角色（内置 / 自定义）</summary>
    public bool IsBuiltin { get; init; }

    /// <summary>已勾选权限点数</summary>
    public int PermissionCount { get; init; }

    /// <summary>绑定用户数</summary>
    public int UserCount { get; init; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; init; }
}
