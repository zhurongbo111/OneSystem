namespace App.Core.Features.Users;

/// <summary>
/// 用户所属角色出参模型（列表 / 详情共用，含 id 与名称用于回显与筛选）
/// </summary>
public sealed class UserRoleDto
{
    /// <summary>角色 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>角色名称</summary>
    public string Name { get; init; } = string.Empty;
}
