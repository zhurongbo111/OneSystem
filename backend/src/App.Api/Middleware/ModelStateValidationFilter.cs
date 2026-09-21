using App.Core.Errors;

using Microsoft.AspNetCore.Mvc.Filters;

namespace App.Api.Middleware;

/// <summary>
/// 绑定 / 反序列化失败收敛：ModelState 非法时统一抛 <see cref="BusinessException"/>（code 40000），
/// 由 <see cref="GlobalExceptionMiddleware"/> 包装为统一响应；原始错误只入日志（含字段与 traceId），不回传客户端。
/// </summary>
/// <remarks>
/// 配合 <c>ApiBehaviorOptions.SuppressModelStateInvalidFilter = true</c> 使用：
/// 关闭后 [ApiController] 不再自动返回 RFC problem-details，但动作方法仍会带非法模型执行（[FromBody] 请求对象为 null），
/// 故必须由此过滤器在动作执行前短路，避免 Handler 收到空请求而抛 NRE（50000）。
/// 业务校验仍由 <c>Core/Mediation/Mediator</c> 统一执行的 FluentValidation 负责，此处不重复校验。
/// </remarks>
public class ModelStateValidationFilter : IActionFilter
{
    private readonly ILogger<ModelStateValidationFilter> _logger;

    /// <summary>
    /// 初始化过滤器
    /// </summary>
    public ModelStateValidationFilter(ILogger<ModelStateValidationFilter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 动作执行前检查模型状态
    /// </summary>
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid)
        {
            return;
        }

        foreach (var (field, entry) in context.ModelState)
        {
            foreach (var error in entry.Errors)
            {
                var raw = string.IsNullOrWhiteSpace(error.ErrorMessage) ? error.Exception?.Message : error.ErrorMessage;
                _logger.LogWarning("请求绑定失败：字段={Field}，原始错误={Error}，路径={Path}", field, raw, context.HttpContext.Request.Path);
            }
        }

        throw new BusinessException(ErrorCode.Validation, "参数错误");
    }

    /// <summary>
    /// 动作执行后无处理
    /// </summary>
    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
