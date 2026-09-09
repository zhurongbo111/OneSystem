using App.Core.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 健康检查接口（白名单放行，无需认证）
/// </summary>
[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// 健康检查
    /// </summary>
    [AllowAnonymous]
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<string>))]
    [HttpGet]
    public ApiResponse<string> GetHealth() => ApiResponseFactory.Ok("healthy");
}
