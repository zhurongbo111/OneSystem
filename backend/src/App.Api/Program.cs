using App.Api.Authentication;
using App.Api.Http;
using App.Api.Middleware;
using App.Api.Observability;
using App.Api.Swagger;
using App.Core;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Infrastructure;
using App.Infrastructure.Persistence;

using NLog;
using NLog.Web;

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
// 关闭 [ApiController] 自动 400（RFC problem-details，破坏统一响应契约），改由 ModelStateValidationFilter 抛 BusinessException，
// 经 GlobalExceptionMiddleware 返回统一响应；业务校验仍由 Mediator 统一执行 FluentValidation，不在此重复
builder.Services.AddControllers(options => options.Filters.Add<ModelStateValidationFilter>())
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

var app = builder.Build();

// ========== 管道：全局异常 → 路由 → 认证/授权 → 控制器（认证失败由 JwtBearerEvents.OnChallenge 统一返回 code 40100）==========
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseRouting();
if (app.Environment.IsDevelopment())
{
    // 置于认证/授权之前：Swagger 端点命中即短路（UI / JSON 匿名可访问），不进入认证管道
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ========== 数据库初始化：迁移（仅 dev 自动执行，生产由发布流程显式执行）+ 内置管理员种子（幂等）==========
await DatabaseInitializer.InitializeAsync(app.Services, app.Environment.IsDevelopment());

app.Logger.LogInformation("App.Api 启动完成，环境={Environment}", app.Environment.EnvironmentName);
app.Run();

/// <summary>
/// 供 WebApplicationFactory 测试使用
/// </summary>
public partial class Program;
