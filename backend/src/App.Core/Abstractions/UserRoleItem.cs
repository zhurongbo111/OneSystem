namespace App.Core.Abstractions;

/// <summary>
/// 用户所属角色读模型（仓储出参契约）：用户档案与角色档案联查得到的角色摘要。
/// 仅供仓储接口 ↔ Mapper 传递，禁止暴露到 API（见后端规则 §4.3）。
/// </summary>
public sealed record UserRoleItem
{
    /// <summary>角色 ID</summary>
    public required Guid Id { get; init; }

    /// <summary>角色名称</summary>
    public required string Name { get; init; }
}
