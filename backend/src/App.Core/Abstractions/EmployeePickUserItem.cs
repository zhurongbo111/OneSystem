namespace App.Core.Abstractions;

/// <summary>
/// 员工可选账号读模型（员工表单「关联账号」下拉用）：
/// 启用且未被任何员工绑定的账号 ∪ 当前员工已绑定的账号
/// （specs/030-erp-org-employee/design.md §3.4）
/// </summary>
public sealed record EmployeePickUserItem
{
    /// <summary>账号 id</summary>
    public required Guid Id { get; init; }

    /// <summary>登录名</summary>
    public required string Username { get; init; }

    /// <summary>账号显示名</summary>
    public required string DisplayName { get; init; }
}
