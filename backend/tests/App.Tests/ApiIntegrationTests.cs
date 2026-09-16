using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using App.Core.Features.Auth.Login;
using App.Core.Features.LoginLogs;
using App.Core.Features.Users;
using App.Core.Features.Users.CreateUser;
using App.Core.Features.Users.ResetPassword;
using App.Core.Features.Users.UpdateUser;
using App.Core.Features.Users.UpdateUserStatus;
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
/// 后端全链路集成测试（WebApplicationFactory 真实 HTTP + InMemory 数据库）
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
        // 固定库名：options 委托按 scope 求值，若在委托内生成随机名会导致种子与查询落到不同库
        private static readonly string DatabaseName = "test-db-" + Guid.NewGuid();

        static Factory()
        {
            // Program 早期的 JWT 密钥校验读取进程环境变量（in-memory 配置对其不可见），
            // Production 环境缺失即启动失败，故在工厂首次解析前提供测试密钥（对 Development 工厂无副作用）
            Environment.SetEnvironmentVariable("JWT__SECRET", new string('k', 48));
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // WebApplicationFactory 下 UseSetting("ASPNETCORE_ENVIRONMENT", ...) 对运行时环境不生效，须用 UseEnvironment 切换
            builder.UseEnvironment("Production");
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
                // 测试环境用 InMemory 数据库替代 PostgreSQL：连同 options 一起移除，避免与 Npgsql 配置叠加
                services.RemoveAll<AppDbContext>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(DatabaseName));
            });
        }
    }

    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>生成不重复的用户名（用户名规则：3-50 位字母 / 数字 / 下划线）</summary>
    private static string UniqueUsername() => "u" + Guid.NewGuid().ToString("N")[..12];

    private static CreateUserRequest NewUserRequest(string username, string password = "user123", string? email = null, string? phone = null)
        => new()
        {
            Username = username,
            DisplayName = $"显示-{username}",
            Email = email,
            Phone = phone,
            Password = password,
        };

    /// <summary>用内置管理员登录并写入 Authorization 头</summary>
    private async Task LoginAsAdminAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = "admin", Password = "admin123" });
        var login = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(_jsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Data!.Token);
    }

    /// <summary>新增用户并断言成功</summary>
    private async Task<UserDetailDto> CreateUserAsync(CreateUserRequest request)
    {
        var response = await _client.PostAsJsonAsync("/api/users", request);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserDetailDto>>(_jsonOptions);
        Assert.Equal(0, result!.Code);
        return result.Data!;
    }

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
        // 密码长度合法（满足统一的 6–32 位约束），仅值错误，才能落到 40001
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = "admin", Password = "wrongPwd" });
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
        await LoginAsAdminAsync();
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

    // ============================== 用户管理 ==============================

    [Fact]
    public async Task 用户列表_无token_返回40100()
    {
        var result = await _client.GetFromJsonAsync<ApiResponse<JsonElement>>("/api/users", _jsonOptions);

        Assert.Equal(40100, result!.Code);
    }

    [Fact]
    public async Task 用户列表_有效token_返回分页结构()
    {
        await LoginAsAdminAsync();

        var result = await _client.GetFromJsonAsync<ApiResponse<PagedResult<UserListItemDto>>>(
            "/api/users?page=1&pageSize=10",
            _jsonOptions);

        Assert.Equal(0, result!.Code);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Page);
        Assert.Equal(10, result.Data.PageSize);
        Assert.True(result.Data.Total >= 1);
        Assert.True(result.Data.Items.Count <= 10);
    }

    [Fact]
    public async Task 用户列表_按关键词筛选_应能查到新增用户()
    {
        await LoginAsAdminAsync();
        var username = UniqueUsername();
        await CreateUserAsync(NewUserRequest(username));

        var result = await _client.GetFromJsonAsync<ApiResponse<PagedResult<UserListItemDto>>>(
            $"/api/users?keyword={username}",
            _jsonOptions);

        Assert.Equal(1, result!.Data!.Total);
        Assert.Equal(username, result.Data.Items[0].Username);
        Assert.Equal(1, result.Data.Items[0].Status);
    }

    [Fact]
    public async Task 新增用户_用户名重复_返回40002()
    {
        await LoginAsAdminAsync();
        var username = UniqueUsername();
        await CreateUserAsync(NewUserRequest(username));

        var response = await _client.PostAsJsonAsync("/api/users", NewUserRequest(username));
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(_jsonOptions);

        Assert.Equal(40002, result!.Code);
    }

    [Fact]
    public async Task 新增用户_邮箱重复_返回40003()
    {
        await LoginAsAdminAsync();
        var email = $"{UniqueUsername()}@example.com";
        await CreateUserAsync(NewUserRequest(UniqueUsername(), email: email));

        var response = await _client.PostAsJsonAsync("/api/users", NewUserRequest(UniqueUsername(), email: email.ToUpperInvariant()));
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(_jsonOptions);

        Assert.Equal(40003, result!.Code);
    }

    [Fact]
    public async Task 新增用户_手机号格式非法_返回40000()
    {
        await LoginAsAdminAsync();

        var response = await _client.PostAsJsonAsync("/api/users", NewUserRequest(UniqueUsername(), phone: "12345"));
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(_jsonOptions);

        Assert.Equal(40000, result!.Code);
    }

    [Fact]
    public async Task 编辑用户_成功_且用户名保持不变()
    {
        await LoginAsAdminAsync();
        var username = UniqueUsername();
        var created = await CreateUserAsync(NewUserRequest(username));

        var response = await _client.PutAsJsonAsync($"/api/users/{created.Id}", new UpdateUserRequest
        {
            DisplayName = "已改名",
            Email = $"{UniqueUsername()}@example.com",
        });
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserDetailDto>>(_jsonOptions);

        Assert.Equal(0, result!.Code);
        Assert.Equal(username, result.Data!.Username);
        Assert.Equal("已改名", result.Data.DisplayName);
        Assert.NotNull(result.Data.Email);
    }

    [Fact]
    public async Task 编辑用户_用户不存在_返回40400()
    {
        await LoginAsAdminAsync();

        var response = await _client.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}", new UpdateUserRequest { DisplayName = "任意" });
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(_jsonOptions);

        Assert.Equal(40400, result!.Code);
    }

    [Fact]
    public async Task 查询用户详情_用户不存在_返回40400()
    {
        await LoginAsAdminAsync();

        var result = await _client.GetFromJsonAsync<ApiResponse<JsonElement>>($"/api/users/{Guid.NewGuid()}", _jsonOptions);

        Assert.Equal(40400, result!.Code);
    }

    [Fact]
    public async Task 禁用当前登录账号_返回40006()
    {
        await LoginAsAdminAsync();
        var me = await _client.GetFromJsonAsync<ApiResponse<UserDto>>("/api/users/me", _jsonOptions);

        var response = await _client.PutAsJsonAsync($"/api/users/{me!.Data!.Id}/status", new UpdateUserStatusRequest { Status = 0 });
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(_jsonOptions);

        Assert.Equal(40006, result!.Code);
    }

    [Fact]
    public async Task 禁用其他用户后_该账号登录返回40005()
    {
        await LoginAsAdminAsync();
        var created = await CreateUserAsync(NewUserRequest(UniqueUsername(), password: "user123"));

        var statusResponse = await _client.PutAsJsonAsync($"/api/users/{created.Id}/status", new UpdateUserStatusRequest { Status = 0 });
        var statusResult = await statusResponse.Content.ReadFromJsonAsync<ApiResponse<UserDetailDto>>(_jsonOptions);
        Assert.Equal(0, statusResult!.Code);
        Assert.Equal(0, statusResult.Data!.Status);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = created.Username, Password = "user123" });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(_jsonOptions);

        Assert.Equal(40005, loginResult!.Code);
    }

    [Fact]
    public async Task 重置密码_新密码可登录_旧密码返回40001()
    {
        await LoginAsAdminAsync();
        var username = UniqueUsername();
        var created = await CreateUserAsync(NewUserRequest(username, password: "user123"));

        var response = await _client.PutAsJsonAsync($"/api/users/{created.Id}/password", new ResetPasswordRequest { NewPassword = "new12345" });
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(_jsonOptions);
        Assert.Equal(0, result!.Code);

        var oldLogin = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = username, Password = "user123" });
        var oldResult = await oldLogin.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(_jsonOptions);
        Assert.Equal(40001, oldResult!.Code);

        var newLogin = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = username, Password = "new12345" });
        var newResult = await newLogin.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(_jsonOptions);
        Assert.Equal(0, newResult!.Code);
        Assert.False(string.IsNullOrEmpty(newResult.Data!.Token));
    }

    // ============================== 登录日志 ==============================

    [Fact]
    public async Task 登录日志_无token_返回40100()
    {
        var result = await _client.GetFromJsonAsync<ApiResponse<JsonElement>>("/api/login-logs", _jsonOptions);

        Assert.Equal(40100, result!.Code);
    }

    [Fact]
    public async Task 登录日志_成功登录后_可查询到该记录()
    {
        await LoginAsAdminAsync();

        var result = await _client.GetFromJsonAsync<ApiResponse<PagedResult<LoginLogListItemDto>>>(
            "/api/login-logs?page=1&pageSize=10",
            _jsonOptions);

        Assert.Equal(0, result!.Code);
        Assert.True(result.Data!.Total >= 1);
        var latest = result.Data.Items[0];
        Assert.Equal("admin", latest.Username);
        Assert.Equal("管理员", latest.DisplayName);
        Assert.NotEqual(Guid.Empty.ToString(), latest.UserId);
        Assert.True((DateTimeOffset.UtcNow - latest.LoginAt).TotalMinutes < 5);
    }

    [Fact]
    public async Task 登录日志_按登录名筛选_应忽略大小写()
    {
        await LoginAsAdminAsync();

        var result = await _client.GetFromJsonAsync<ApiResponse<PagedResult<LoginLogListItemDto>>>(
            "/api/login-logs?username=ADMIN",
            _jsonOptions);

        Assert.Equal(0, result!.Code);
        Assert.True(result.Data!.Total >= 1);
        Assert.All(result.Data.Items, item => Assert.Equal("admin", item.Username));
    }

    [Fact]
    public async Task 登录日志_按时间范围筛选_应包含当天记录()
    {
        await LoginAsAdminAsync();
        var today = DateTime.UtcNow.Date;
        var query = $"/api/login-logs?startTime={today:yyyy-MM-ddTHH:mm:ss.fffZ}&endTime={today.AddDays(1):yyyy-MM-ddTHH:mm:ss.fffZ}";

        var result = await _client.GetFromJsonAsync<ApiResponse<PagedResult<LoginLogListItemDto>>>(query, _jsonOptions);

        Assert.Equal(0, result!.Code);
        Assert.True(result.Data!.Total >= 1);
    }

    [Fact]
    public async Task 登录日志_开始时间晚于结束时间_返回40000()
    {
        await LoginAsAdminAsync();
        var today = DateTime.UtcNow.Date;

        var result = await _client.GetFromJsonAsync<ApiResponse<JsonElement>>(
            $"/api/login-logs?startTime={today.AddDays(1):yyyy-MM-ddTHH:mm:ss.fffZ}&endTime={today:yyyy-MM-ddTHH:mm:ss.fffZ}",
            _jsonOptions);

        Assert.Equal(40000, result!.Code);
    }
}
