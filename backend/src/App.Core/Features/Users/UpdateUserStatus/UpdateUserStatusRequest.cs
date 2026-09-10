using App.Core.Abstractions;

namespace App.Core.Features.Users.UpdateUserStatus;

/// <summary>
/// 启用 / 禁用用户请求
/// </summary>
public sealed class UpdateUserStatusRequest : IRequest<UserDetailDto>
{
    /// <summary>用户 id（以路由 id 为准）</summary>
    public Guid Id { get; init; }

    /// <summary>目标状态：1 启用 / 0 禁用</summary>
    public int Status { get; init; }
}
