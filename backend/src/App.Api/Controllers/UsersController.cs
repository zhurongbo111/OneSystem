using App.Core.Dtos;
using App.Core.Handlers;
using App.Core.Responses;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 用户接口
/// </summary>
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserHandler _userHandler;

    /// <summary>
    /// 初始化用户控制器
    /// </summary>
    public UsersController(IUserHandler userHandler)
    {
        _userHandler = userHandler;
    }

    /// <summary>
    /// 获取当前登录用户
    /// </summary>
    [HttpGet("me")]
    public ApiResponse<UserDto> Me()
    {
        var user = _userHandler.GetCurrentUser(User);
        return ApiResponseFactory.Ok(user);
    }
}
