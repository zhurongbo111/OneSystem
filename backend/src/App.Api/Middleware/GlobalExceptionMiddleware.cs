using System.Diagnostics;
using App.Core.Errors;
using App.Core.Responses;

namespace App.Api.Middleware;

/// <summary>
/// 全局异常中间件：BusinessException 按其 Code 返回，其他异常统一 50000，日志包含 traceId 上下文
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    /// <summary>
    /// 初始化中间件
    /// </summary>
    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// 处理请求管道
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning("业务异常：{Code} {Message}，traceId={TraceId}", ex.Code, ex.Message, Activity.Current?.TraceId);
            await WriteAsync(context, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "未处理异常，traceId={TraceId}", Activity.Current?.TraceId);
            await WriteAsync(context, ErrorCode.Internal, "服务内部错误");
        }
    }

    private static async Task WriteAsync(HttpContext context, int code, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(ApiResponseFactory.Fail(code, message));
    }
}
