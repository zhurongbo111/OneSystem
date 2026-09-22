namespace App.Core.Abstractions;

/// <summary>
/// 角色列表读模型（仓储出参契约）：角色实体字段 + 权限数 / 用户数两个联查字段。
/// 仅供仓储接口 ↔ Mapper 传递，禁止暴露到 API（见后端规则 §4.3）。
/// </summary>
public sealed record RoleListItem
{
    /// <summary>角色 ID</summary>
    public required Guid Id { get; init; }

    /// <summary>角色名称</summary>
    public required string Name { get; init; }

    /// <summary>备注</summary>
    public string? Remark { get; init; }

    /// <summary>是否内置角色</summary>
    public required bool IsBuiltin { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>该角色已勾选的权限点数量</summary>
    public required int PermissionCount { get; init; }

    /// <summary>绑定该角色的用户数量</summary>
    public required int UserCount { get; init; }
}
