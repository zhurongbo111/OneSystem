using App.Core.Abstractions;

namespace App.Core.Features.Users.GetUserById;

/// <summary>
/// 用户详情请求（id 由路由传入）
/// </summary>
public sealed class GetUserByIdRequest : IRequest<UserDetailDto>
{
    /// <summary>用户 id</summary>
    public Guid Id { get; init; }
}
