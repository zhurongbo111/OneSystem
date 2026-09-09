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
/// 后端全链路集成测试（WebApplicationFactory 真实 HTTP）
/// </summary>
public class ApiIntegrationTests : IClassFixture<ApiIntegrationTests.Factory>
{
    public ApiIntegrationTests(Factory factory)
    {
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    public class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Production");
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
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("test-db-" + Guid.NewGuid()));
            });
        }
    }

    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    [Fact]
    public async Task 健康检查_无需认证_返回成功()
    {
        var response = await _client.GetFromJsonAsync<ApiResponse<string>>("/health", _jsonOptions);

        Assert.NotNull(response);
        Assert.Equal(0, response!.Code);
        Assert.Equal("healthy", response.Data);
    }

    [Fact]
    public async Task 登录_正确账号_返回token与用户()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = "admin", Password = "admin123" });
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(_jsonOptions);

        Assert.NotNull(result);
        Assert.Equal(0, result!.Code);
        Assert.False(string.IsNullOrEmpty(result.Data!.Token));
        Assert.Equal("admin", result.Data.User.Username);
    }

    [Fact]
    public async Task 登录_错误密码_返回40001()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = "admin", Password = "wrong" });
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(_jsonOptions);

        Assert.Equal(40001, result!.Code);
        Assert.Equal("用户名或密码错误", result.Message);
    }

    [Fact]
    public async Task 登录_参数为空_返回40000()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = "", Password = "" });
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(_jsonOptions);

        Assert.Equal(40000, result!.Code);
    }

    [Fact]
    public async Task 获取当前用户_无token_返回40100()
    {
        var result = await _client.GetFromJsonAsync<ApiResponse<JsonElement>>("/api/users/me", _jsonOptions);

        Assert.Equal(40100, result!.Code);
    }

    [Fact]
    public async Task 获取当前用户_有效token_返回用户()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = "admin", Password = "admin123" });
        var login = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(_jsonOptions);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Data!.Token);
        var result = await _client.GetFromJsonAsync<ApiResponse<UserDto>>("/api/users/me", _jsonOptions);

        Assert.Equal(0, result!.Code);
        Assert.Equal("admin", result.Data!.Username);
        Assert.Equal("管理员", result.Data.DisplayName);
    }

    [Fact]
    public async Task 获取当前用户_伪造token_返回40100()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "forged.invalid.token");
        var result = await _client.GetFromJsonAsync<ApiResponse<JsonElement>>("/api/users/me", _jsonOptions);

        Assert.Equal(40100, result!.Code);
    }
}
