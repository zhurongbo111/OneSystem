using App.Core.Features.Auth.Login;

using Microsoft.OpenApi.Models;

namespace App.Api.Swagger;

/// <summary>
/// Swagger 服务注册（从 Program.cs 拆分）
/// </summary>
internal static class SwaggerRegistration
{
    /// <summary>
    /// 仅 dev 环境注册 Swagger（UI /swagger，JSON /swagger/v1/swagger.json；prod 零注册零暴露）
    /// </summary>
    public static IServiceCollection AddSwaggerIfDevelopment(this IServiceCollection services, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            return services;
        }

        var apiXmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
        var coreXmlFile = $"{typeof(LoginRequest).Assembly.GetName().Name}.xml";
        services.AddSwaggerGen(options =>
        {
            // 引入 Controller 动作与 Request/Response 模型的 XML 注释
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, apiXmlFile));
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, coreXmlFile));

            options.SwaggerDoc("v1", new OpenApiInfo { Title = "App API", Version = "v1" });

            // JWT Bearer 安全方案：UI 右上角 Authorize 粘贴 token 后可在文档页调用受保护接口
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "粘贴登录接口签发的 JWT（不带 Bearer 前缀亦可）",
            });
            // 不使用文档级 AddSecurityRequirement（其作用于全部 operation，无法区分匿名接口）；
            // security 要求由 Filter 按 [AllowAnonymous] 白名单语义逐 operation 标注，UI 锁图标与真实认证一致
            options.OperationFilter<SwaggerSecurityOperationFilter>();
        });

        return services;
    }
}
