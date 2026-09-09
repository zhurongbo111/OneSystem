using System.Diagnostics;
using App.Api.Authentication;
using App.Api.Middleware;
using App.Core;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Web;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.Runtime;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// ========== 日志（NLog 对接 ILogger<T>，级别：dev=Info / prod=Warning，见 nlog.config）==========
builder.Host.UseNLog();

// ========== JWT 配置（密钥等敏感项从环境变量注入，dev 缺失时生成随机兜底密钥）==========
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwtSecret = builder.Configuration[JwtOptions.SectionName + ":Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("生产环境必须通过环境变量 JWT__SECRET 配置 JWT 签名密钥");
    }

    jwtSecret = TokenService.GenerateDevSecret();
    builder.Configuration[JwtOptions.SectionName + ":Secret"] = jwtSecret;
    LogManager.GetCurrentClassLogger().Warn("未配置 JWT__SECRET，已使用开发环境随机兜底密钥（进程重启后已签发 token 失效）");
}

// ========== 服务注册 ==========
builder.Services.AddCore();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ASP.NET Core 默认 JWT 认证：校验参数与签发共用 JwtOptions，FallbackPolicy 默认要求登录（白名单接口用 [AllowAnonymous] 标注）
builder.Services.AddJwtAuthentication(builder.Configuration);

// 当前用户（claims → ICurrentUser），供需要当前用户的用例 Handler 使用
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserAccessor>();

// ========== OpenTelemetry：Tracing + Metrics 自动埋点，OTLP 仅在配置 OTEL_EXPORTER_OTLP_ENDPOINT 时导出 ==========
var otelEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
var otlpEnabled = !string.IsNullOrWhiteSpace(otelEndpoint);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("app-api", "1.0.0"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddEntityFrameworkCoreInstrumentation();
        if (otlpEnabled)
        {
            tracing.AddOtlpExporter();
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation();
        if (otlpEnabled)
        {
            metrics.AddOtlpExporter();
        }
    });

var app = builder.Build();

// ========== 管道：全局异常 → 路由 → 认证/授权 → 控制器（认证失败由 JwtBearerEvents.OnChallenge 统一返回 code 40100）==========
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Logger.LogInformation("App.Api 启动完成，环境={Environment}", app.Environment.EnvironmentName);
app.Run();

/// <summary>
/// 供 WebApplicationFactory 测试使用
/// </summary>
public partial class Program;
