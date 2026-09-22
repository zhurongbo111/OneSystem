namespace App.Core.Features.Permissions;

/// <summary>
/// 权限点分组出参模型（供角色抽屉渲染权限树）
/// </summary>
public sealed class PermissionGroupDto
{
    /// <summary>分组名称（如「商品管理」）</summary>
    public string GroupName { get; init; } = string.Empty;

    /// <summary>该分组下的权限点</summary>
    public IReadOnlyList<PermissionItemDto> Items { get; init; } = [];
}

/// <summary>
/// 权限点出参模型
/// </summary>
public sealed class PermissionItemDto
{
    /// <summary>权限点 key（如 <c>products.view</c>）</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>权限点中文名（供权限树展示，前端不硬编码）</summary>
    public string Name { get; init; } = string.Empty;
}
