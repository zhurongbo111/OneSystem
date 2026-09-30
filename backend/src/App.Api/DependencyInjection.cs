
using App.Api.Authentication;
using App.Api.Authorization;
using App.Api.Http;
using App.Api.Middleware;
using App.Api.Observability;
using App.Api.Swagger;
using App.Core.Abstractions;

namespace App.Api;

/// <summary>
/// App.Api 服务注册扩展
/// </summary>
public static class DependencyInjection
{
    public static WebApplicationBuilder AddApi(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        // 关闭 [ApiController] 自动 400（RFC problem-details，破坏统一响应契约），改由 ModelStateValidationFilter 抛 BusinessException，
        // 经 GlobalExceptionMiddleware 返回统一响应；业务校验仍由 Mediator 统一执行 FluentValidation，不在此重复
        builder.Services.AddControllers(options =>
        {
            options.Filters.Add<ModelStateValidationFilter>();
            // 权限校验（erp-rbac）：所有动作统一在此校验，未标注 [RequirePermission] 且不在白名单内的动作会被拒绝（40300）
            options.Filters.Add<PermissionAuthorizationFilter>();
        })
            .ConfigureApiBehaviorOptions(options => options.SuppressModelStateInvalidFilter = true);
        builder.Services.AddEndpointsApiExplorer();

        // ASP.NET Core 默认 JWT 认证：校验参数与签发共用 JwtOptions，FallbackPolicy 默认要求登录（白名单接口用 [AllowAnonymous] 标注）
        builder.Services.AddJwtAuthentication(builder.Configuration);

        // 请求上下文抽象（claims → ICurrentUser / HttpContext → IClientInfo），供用例 Handler 使用
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, CurrentUserAccessor>();
        builder.Services.AddScoped<IClientInfo, ClientInfoAccessor>();

        // ========== Swagger：仅 dev 环境启用（UI /swagger，JSON /swagger/v1/swagger.json；prod 零注册零暴露），配置见 Swagger/SwaggerRegistration.cs ==========
        builder.Services.AddSwaggerIfDevelopment(builder.Environment);

        // ========== OpenTelemetry：Tracing + Metrics 自动埋点，OTLP 仅在配置 OTEL_EXPORTER_OTLP_ENDPOINT 时导出，配置见 Observability/OpenTelemetryRegistration.cs ==========
        builder.Services.AddTelemetry();
        return builder;
    }
}