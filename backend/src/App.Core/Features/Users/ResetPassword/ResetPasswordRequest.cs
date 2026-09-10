using App.Core.Abstractions;

namespace App.Core.Features.Users.ResetPassword;

/// <summary>
/// 重置用户密码请求（管理员操作，无需原密码）
/// </summary>
public sealed class ResetPasswordRequest : IRequest<object?>
{
    /// <summary>用户 id（以路由 id 为准）</summary>
    public Guid Id { get; init; }

    /// <summary>新密码（明文，仅用于入参；服务端哈希后入库）</summary>
    public string NewPassword { get; init; } = string.Empty;
}
