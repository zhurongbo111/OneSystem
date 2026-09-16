using System.Diagnostics;
using System.Text;

using App.Core.Auth;
using App.Core.Errors;
using App.Core.Responses;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace App.Api.Authentication;

/// <summary>
/// ASP.NET Core 默认 JwtBearer 认证注册扩展。
/// 校验配置与签发共用 JwtOptions；未认证统一返回 code 40100（HTTP 200，保持全站统一响应约定）。
/// </summary>
internal static class JwtBearerExtensions
{
    /// <summary>
    /// 注册 JWT 认证与授权（FallbackPolicy 默认要求登录，白名单接口显式标注 [AllowAnonymous]）。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置（含 Jwt 配置节，密钥已由 Program 完成 dev 兜底 / prod 缺失校验）</param>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = HandleAuthenticationFailed,
                    OnChallenge = HandleChallengeAsync,
                };
            });

        // 默认要求登录：除显式标注 [AllowAnonymous] 的接口外，全部需要有效 token（等价原"白名单外全局校验"）
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    private static Task HandleAuthenticationFailed(AuthenticationFailedContext context)
    {
        var logger = context.HttpContext.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("JwtBearer");
        logger?.LogWarning("token 校验失败：{Error}，traceId={TraceId}", context.Exception?.Message, Activity.Current?.TraceId);
        return Task.CompletedTask;
    }

    private static Task HandleChallengeAsync(JwtBearerChallengeContext context)
    {
        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        context.HandleResponse();
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsJsonAsync(ApiResponseFactory.Fail(ErrorCode.Unauthorized, "未登录或 token 无效"));
    }
}
