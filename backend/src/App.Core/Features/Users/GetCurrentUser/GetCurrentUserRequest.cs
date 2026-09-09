using App.Core.Abstractions;

namespace App.Core.Features.Users.GetCurrentUser;

/// <summary>
/// 获取当前用户请求（实现 <see cref="IRequest{TResponse}"/> 标记；无请求参数，
/// 空模型仅用于统一 RequestHandler 入口签名，不定义 Validator）
/// </summary>
public sealed class GetCurrentUserRequest : IRequest<UserDto>
{
}
