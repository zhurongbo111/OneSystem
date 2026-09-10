using App.Core.Abstractions;

namespace App.Core.Features.Users.CreateUser;

/// <summary>
/// 新增用户请求（管理员在后台创建，无注册入口；密码由管理员设置初始值）
/// </summary>
public sealed class CreateUserRequest : IRequest<UserDetailDto>
{
    /// <summary>登录名（唯一，创建后不可修改）</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>显示名称</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>邮箱，可空；非空时唯一</summary>
    public string? Email { get; init; }

    /// <summary>手机号，可空；非空时唯一</summary>
    public string? Phone { get; init; }

    /// <summary>初始密码（明文，仅用于入参；服务端哈希后入库）</summary>
    public string Password { get; init; } = string.Empty;
}
