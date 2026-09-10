using App.Core.Abstractions;

namespace App.Core.Features.Users.UpdateUser;

/// <summary>
/// 编辑用户请求（用户名创建后不可修改，故不含用户名字段）
/// </summary>
public sealed class UpdateUserRequest : IRequest<UserDetailDto>
{
    /// <summary>用户 id（以路由 id 为准）</summary>
    public Guid Id { get; init; }

    /// <summary>显示名称</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>邮箱，可空；非空时唯一（排除自身）</summary>
    public string? Email { get; init; }

    /// <summary>手机号，可空；非空时唯一（排除自身）</summary>
    public string? Phone { get; init; }
}
