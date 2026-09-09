using App.Core.Dtos;
using App.Core.Errors;
using App.Core.Handlers;
using App.Core.Responses;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 认证接口
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthHandler _authHandler;

    /// <summary>
    /// 初始化认证控制器
    /// </summary>
    public AuthController(IAuthHandler authHandler)
    {
        _authHandler = authHandler;
    }

    /// <summary>
    /// 登录，签发 JWT
    /// </summary>
    [HttpPost("login")]
    public async Task<ApiResponse<LoginResult>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ApiResponseFactory.Fail<LoginResult>(ErrorCode.Validation, "用户名或密码不能为空");
        }

        var result = await _authHandler.LoginAsync(request, cancellationToken);
        return ApiResponseFactory.Ok(result);
    }
}
