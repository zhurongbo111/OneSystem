using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using App.Core.Features.Auth.Login;
using App.Core.Features.Users;
using App.Core.Responses;
using App.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace App.Tests;

/// <summary>
/// Swagger 集成测试：dev 环境 UI / JSON 可匿名访问、Bearer 安全方案、受保护接口 401 标注；Production 不暴露
/// </summary>
public class SwaggerIntegrationTests : IClassFixture<SwaggerIntegrationTests.DevFactory>
{
    public SwaggerIntegrationTests(DevFactory factory)
    {
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    /// <summary>
    /// Development 环境工厂（Swagger 仅在 dev 启用）
    /// </summary>
    public class DevFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Secret"] = new string('k', 48),
                    ["Jwt:Issuer"] = "app-api",
                    ["Jwt:Audience"] = "app-web",
                    ["Jwt:ExpiresMinutes"] = "120",
                });
            });

            builder.ConfigureServices(services =>
            {
                // 测试环境用 InMemory 数据库替代 PostgreSQL
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("test-db-swagger-" + Guid.NewGuid()));
            });
        }
    }

    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    [Fact]
    public async Task SwaggerJson_dev环境_可匿名访问且包含Bearer安全方案()
    {
        var json = await _client.GetStringAsync("/swagger/v1/swagger.json");
        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("components", out var components), "swagger.json 缺少 components 节点");
        Assert.True(components.TryGetProperty("securitySchemes", out var schemes), "swagger.json 缺少 securitySchemes 节点");
        Assert.True(schemes.TryGetProperty("Bearer", out var bearer), "swagger.json 缺少 Bearer 安全方案");
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.Equal("http", bearer.GetProperty("type").GetString());
    }

    [Fact]
    public async Task SwaggerJson_dev环境_受保护接口标注401()
    {
        var json = await _client.GetStringAsync("/swagger/v1/swagger.json");
        using var doc = JsonDocument.Parse(json);

        var pathItem = doc.RootElement.GetProperty("paths").GetProperty("/api/users/me");
        var getOperation = pathItem.GetProperty("get");
        var responses = getOperation.GetProperty("responses");

        Assert.True(responses.TryGetProperty("401", out _), "受保护接口缺少 401 响应标注");
    }

    [Fact]
    public async Task SwaggerUi_dev环境_可匿名打开()
    {
        var response = await _client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("swagger-ui", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Swagger_dev环境_带token调用受保护接口_返回用户()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = "admin", Password = "admin123" });
        var login = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(_jsonOptions);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Data!.Token);
        var result = await _client.GetFromJsonAsync<ApiResponse<UserDto>>("/api/users/me", _jsonOptions);

        Assert.Equal(0, result!.Code);
        Assert.Equal("admin", result.Data!.Username);
    }

    [Fact]
    public async Task Swagger_Production环境_文档不可访问()
    {
        // 进程级提供 JWT__SECRET：Production 启动校验在 Program 早期读取环境变量，需在构建前设置（对 Development 测试无副作用）
        Environment.SetEnvironmentVariable("JWT__SECRET", new string('p', 48));
        using var productionFactory = new ProductionFactory();
        using var productionClient = productionFactory.CreateClient();

        var response = await productionClient.GetAsync("/swagger/v1/swagger.json");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        // prod 下 Swagger 中间件未注册，路径回落到认证挑战，按全站约定返回 code 40100；且响应不是 OpenAPI 文档
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(40100, doc.RootElement.GetProperty("code").GetInt32());
        Assert.False(doc.RootElement.TryGetProperty("openapi", out _), "prod 不应返回 OpenAPI 文档");
    }

    /// <summary>
    /// Production 环境工厂：UseEnvironment 强制切换（WebApplicationFactory 下 UseSetting 设置 ASPNETCORE_ENVIRONMENT 不生效），验证 prod 下 Swagger 零注册
    /// </summary>
    public class ProductionFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Secret"] = new string('p', 48),
                    ["Jwt:Issuer"] = "app-api",
                    ["Jwt:Audience"] = "app-web",
                    ["Jwt:ExpiresMinutes"] = "120",
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("test-db-swagger-prod-" + Guid.NewGuid()));
            });
        }
    }
}
