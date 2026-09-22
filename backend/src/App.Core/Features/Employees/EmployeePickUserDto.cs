namespace App.Core.Features.Employees;

/// <summary>
/// 员工可选账号出参模型（员工表单「关联账号」下拉用）
/// </summary>
public sealed class EmployeePickUserDto
{
    /// <summary>账号 id</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>登录名</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>账号显示名</summary>
    public string DisplayName { get; init; } = string.Empty;
}
