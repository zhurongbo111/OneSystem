using App.Api;
using App.Api.Middleware;
using App.Core;
using App.Infrastructure;
using App.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ========== 核心服务注册 ==========
builder.Services.AddCore();
// ========== 基础设施注册 ==========
builder.Services.AddInfrastructure(builder.Configuration);
// ========== API服务注册 ==========
builder.AddApi();

var app = builder.Build();

ConfigureApp(app);

// ========== 数据库初始化：迁移+ 内置管理员种子（幂等）==========
await DatabaseInitializer.InitializeAsync(app.Services);
app.Logger.LogInformation("App.Api 启动完成，环境={Environment}", app.Environment.EnvironmentName);
app.Run();

/// <summary>
/// 供 WebApplicationFactory 测试使用
/// </summary>
public partial class Program
{
    private static void ConfigureApp(WebApplication app)
    {
        // ========== 管道：全局异常 → 路由 → 认证/授权 → 控制器（认证失败由 JwtBearerEvents.OnChallenge 统一返回 code 40100）==========
        app.UseStaticFiles();
        if (app.Environment.IsDevelopment())
        {
            // 置于认证/授权之前：Swagger 端点命中即短路（UI / JSON 匿名可访问），不进入认证管道
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseMiddleware<GlobalExceptionMiddleware>();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapFallbackToFile("index.html").AllowAnonymous();
    }
};
