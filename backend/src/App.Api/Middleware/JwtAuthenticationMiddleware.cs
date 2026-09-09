using App.Core.Auth;
using App.Core.Errors;
using App.Core.Responses;
using Microsoft.Extensions.Options;

namespace App.Api.Middleware;

/// <summary>
/// JWT 认证中间件：白名单放行；无 token / token 无效统一返回 code 40100；校验通过写入 HttpContext.User
/// </summary>
public class JwtAuthenticationMiddleware
{
    /// <summary>白名单路径前缀（登录等接口放行）</summary>
    private static readonly string[] Whitelist = { "/api/auth/login", "/health" };

    private readonly RequestDelegate _next;
    private readonly TokenService _tokenService;
    private readonly ILogger<JwtAuthenticationMiddleware> _logger;

    /// <summary>
    /// 初始化中间件
    /// </summary>
    public JwtAuthenticationMiddleware(RequestDelegate next, TokenService tokenService, ILogger<JwtAuthenticationMiddleware> logger)
    {
        _next = next;
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>
    /// 处理请求管道
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (Whitelist.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var header = context.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (string.IsNullOrEmpty(header) || !header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        var token = header[prefix.Length..].Trim();
        var principal = _tokenService.Validate(token);
        if (principal is null)
        {
            _logger.LogWarning("token 校验失败，traceId={TraceId}", System.Diagnostics.Activity.Current?.TraceId);
            await WriteUnauthorizedAsync(context);
            return;
        }

        context.User = principal;
        await _next(context);
    }

    private static async Task WriteUnauthorizedAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(ApiResponseFactory.Fail(ErrorCode.Unauthorized, "未登录或 token 无效"));
    }
}
