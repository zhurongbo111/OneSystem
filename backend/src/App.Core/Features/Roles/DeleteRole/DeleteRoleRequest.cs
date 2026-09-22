using App.Core.Abstractions;

namespace App.Core.Features.Roles.DeleteRole;

/// <summary>
/// 删除角色请求（路由 id）；内置角色与被用户绑定的角色不可删除
/// </summary>
public sealed class DeleteRoleRequest : IRequest<object?>
{
    /// <summary>角色 id</summary>
    public Guid Id { get; init; }
}
