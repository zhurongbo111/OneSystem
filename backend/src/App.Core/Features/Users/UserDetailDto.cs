namespace App.Core.Features.Users;

/// <summary>
/// 用户详情出参模型（新增 / 编辑 / 详情 / 启停共用）
/// </summary>
public sealed class UserDetailDto
{
    /// <summary>用户 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>用户名</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>显示名称</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>邮箱</summary>
    public string? Email { get; init; }

    /// <summary>手机号</summary>
    public string? Phone { get; init; }

    /// <summary>状态（1 启用 / 0 禁用）</summary>
    public int Status { get; init; }

    /// <summary>最近登录时间</summary>
    public DateTimeOffset? LastLoginAt { get; init; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>用户绑定的角色（按名称升序；至少一个角色，见 specs/028-erp-rbac §0.3）</summary>
    public IReadOnlyList<UserRoleDto> Roles { get; init; } = [];
}
