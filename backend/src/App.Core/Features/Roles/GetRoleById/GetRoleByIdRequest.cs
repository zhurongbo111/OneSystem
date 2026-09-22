using App.Core.Abstractions;

namespace App.Core.Features.Roles.GetRoleById;

/// <summary>
/// 角色详情请求（路由 id）
/// </summary>
public sealed class GetRoleByIdRequest : IRequest<RoleDetailDto>
{
    /// <summary>角色 id</summary>
    public Guid Id { get; init; }
}
