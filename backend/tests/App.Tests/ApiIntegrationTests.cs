using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using App.Core.Auth;
using App.Core.Errors;
using App.Core.Features.Auth.Login;
using App.Core.Features.LoginLogs;
using App.Core.Features.Permissions;
using App.Core.Features.Roles;
using App.Core.Features.Roles.CreateRole;
using App.Core.Features.Roles.UpdateRole;
using App.Core.Features.Users;
using App.Core.Features.Users.CreateUser;
using App.Core.Features.Users.ResetPassword;
using App.Core.Features.Users.UpdateUser;
using App.Core.Features.Users.UpdateUserStatus;
using App.Core.Responses;
using App.Infrastructure;

using ClosedXML.Excel;

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

    /// <summary>内置员工角色 id（登录时查询得到）</summary>
    private Guid _staffRoleId;

    /// <summary>生成不重复的用户名（用户名规则：3-50 位字母 / 数字 / 下划线）</summary>
    private static string UniqueUsername() => "u" + Guid.NewGuid().ToString("N")[..12];

    private CreateUserRequest NewUserRequest(string username, string password = "user123", string? email = null, string? phone = null)
        => new()
        {
            Username = username,
            DisplayName = $"显示-{username}",
            Email = email,
            Phone = phone,
            Password = password,
            // 用户必须绑定至少一个角色（避免"无角色用户"黑洞）
            RoleIds = [_staffRoleId],
        };

    /// <summary>用内置管理员登录并写入 Authorization 头</summary>
    private async Task LoginAsAdminAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = "admin", Password = "admin123" });
        var login = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(_jsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Data!.Token);

        // 登录后即有 roles.view 权限，可查内置员工角色，供后续"新增 / 编辑用户"绑定使用
        var roles = await _client.GetFromJsonAsync<ApiResponse<PagedResult<RoleListItemDto>>>(
            "/api/roles?keyword=Staff",
            _jsonOptions);
        _staffRoleId = Guid.Parse(roles!.Data!.Items[0].Id);
    }

    /// <summary>新增用户并断言成功</summary>
    private async Task<UserDetailDto> CreateUserAsync(CreateUserRequest request)
    {
        var response = await _client.PostAsJsonAsync("/api/users", request);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserDetailDto>>(_jsonOptions);
        Assert.Equal(0, result!.Code);
        return result.Data!;
    }

    /// <summary>分页结构简单钩子（角色列表集成断言复用）</summary>
    private async Task<ApiResponse<PagedResult<RoleListItemDto>>> GetRolesAsync(string query)
        => (await _client.GetFromJsonAsync<ApiResponse<PagedResult<RoleListItemDto>>>($"/api/roles{query}", _jsonOptions))!;

    /// <summary>GET 并解包统一响应（断言业务成功）</summary>
    private async Task<ApiResponse<T>> GetAsync<T>(string url)
    {
        var response = await _client.GetAsync(url);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(_jsonOptions);
        Assert.Equal(0, result!.Code);
        return result;
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
        // 内置管理员为超级管理员：登录即返回全量权限点
        Assert.Equal(App.Core.Auth.Permissions.All.Count, result.Data.Permissions.Count);
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
    public async Task 登录_请求体类型不匹配_返回统一响应40000()
    {
        // username 传数字：JSON 反序列化失败 → 由 ModelStateValidationFilter 收敛为 40000，而非 ASP.NET Core 默认 problem-details
        var response = await _client.PostAsync(
            "/api/auth/login",
            new StringContent("{\"username\":123,\"password\":\"admin123\"}", Encoding.UTF8, "application/json"));
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(_jsonOptions);

        Assert.Equal(40000, result!.Code);
        Assert.Equal("参数错误", result.Message);
    }

    [Fact]
    public async Task 登录日志_page参数非数字_返回统一响应40000()
    {
        await LoginAsAdminAsync();

        var result = await _client.GetFromJsonAsync<ApiResponse<JsonElement>>("/api/login-logs?page=abc", _jsonOptions);

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
    public async Task 内置数据_应创建内置角色并为管理员绑定超级管理员()
    {
        await LoginAsAdminAsync();

        var roles = await GetRolesAsync(string.Empty);

        Assert.True(roles.Data!.Total >= 2);
        Assert.Contains(roles.Data.Items, item => item.Name == App.Core.BuiltinRoles.SuperAdmin && item.IsBuiltin);
        Assert.Contains(roles.Data.Items, item => item.Name == App.Core.BuiltinRoles.Staff && item.IsBuiltin);

        var me = await GetAsync<UserDto>("/api/users/me");
        var meDetail = await GetAsync<UserDetailDto>($"/api/users/{me.Data!.Id}");
        Assert.Equal(App.Core.BuiltinRoles.SuperAdmin, meDetail.Data!.Roles[0].Name);
    }

    [Fact]
    public async Task 权限清单_应返回分组且覆盖全部权限点()
    {
        await LoginAsAdminAsync();

        var result = await GetAsync<IReadOnlyList<PermissionGroupDto>>("/api/permissions");

        Assert.Equal(App.Core.Auth.Permissions.Groups.Count, result.Data!.Count);
        Assert.Equal(App.Core.Auth.Permissions.All.Count, result.Data.Sum(group => group.Items.Count));
    }

    [Fact]
    public async Task 当前用户权限_应返回该用户的权限点()
    {
        await LoginAsAdminAsync();

        var result = await GetAsync<IReadOnlyList<string>>("/api/users/me/permissions");

        Assert.Equal(App.Core.Auth.Permissions.All.Count, result.Data!.Count);
    }

    [Fact]
    public async Task 新增角色_名称重复_返回40173()
    {
        await LoginAsAdminAsync();

        var response = await _client.PostAsJsonAsync("/api/roles", new CreateRoleRequest
        {
            Name = App.Core.BuiltinRoles.Staff,
            Remark = null,
            PermissionKeys = [Permissions.PurchasesView],
        });
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<RoleDetailDto>>(_jsonOptions);

        Assert.Equal(ErrorCode.RoleNameExists, result!.Code);
    }

    [Fact]
    public async Task 新增角色_权限点非法_返回40000()
    {
        await LoginAsAdminAsync();

        var response = await _client.PostAsJsonAsync("/api/roles", new CreateRoleRequest
        {
            Name = "测试角色" + UniqueUsername()[..6],
            PermissionKeys = ["products.fly"],
        });
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<RoleDetailDto>>(_jsonOptions);

        Assert.Equal(ErrorCode.Validation, result!.Code);
    }

    [Fact]
    public async Task 角色增删改查_应全链路可用()
    {
        await LoginAsAdminAsync();
        var name = "ROLE" + Guid.NewGuid().ToString("N")[..8];

        var created = await _client.PostAsJsonAsync("/api/roles", new CreateRoleRequest
        {
            Name = name,
            Remark = "集成测试角色",
            PermissionKeys = [Permissions.PurchasesView, Permissions.PurchasesExport],
        });
        var createdResult = await created.Content.ReadFromJsonAsync<ApiResponse<RoleDetailDto>>(_jsonOptions);
        Assert.Equal(0, createdResult!.Code);
        Assert.Equal(2, createdResult.Data!.PermissionKeys.Count);

        var detail = await GetAsync<RoleDetailDto>($"/api/roles/{createdResult.Data.Id}");
        Assert.Equal(name, detail.Data!.Name);
        Assert.Equal(0, detail.Data.UserCount);

        var updated = await _client.PutAsJsonAsync($"/api/roles/{createdResult.Data.Id}", new UpdateRoleRequest
        {
            Name = name,
            Remark = "改过备注",
            PermissionKeys = [Permissions.PurchasesView],
        });
        var updatedResult = await updated.Content.ReadFromJsonAsync<ApiResponse<RoleDetailDto>>(_jsonOptions);
        Assert.Equal(0, updatedResult!.Code);
        Assert.Single(updatedResult.Data!.PermissionKeys);
        Assert.Equal("改过备注", updatedResult.Data.Remark);

        var deleted = await _client.DeleteAsync($"/api/roles/{createdResult.Data.Id}");
        var deletedResult = await deleted.Content.ReadFromJsonAsync<ApiResponse<object?>>(_jsonOptions);
        Assert.Equal(0, deletedResult!.Code);
    }

    [Fact]
    public async Task 删除内置角色_返回40175()
    {
        await LoginAsAdminAsync();
        var staffRoleId = (await GetRolesAsync("?keyword=Staff")).Data!.Items[0].Id;

        var response = await _client.DeleteAsync($"/api/roles/{staffRoleId}");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object?>>(_jsonOptions);

        Assert.Equal(ErrorCode.RoleBuiltinImmutable, result!.Code);
    }

    [Fact]
    public async Task 删除已绑定用户的角色_返回40174()
    {
        await LoginAsAdminAsync();

        // 新建一个非内置角色并绑定给用户，删除应因占用被拒
        var createdRole = await _client.PostAsJsonAsync("/api/roles", new CreateRoleRequest
        {
            Name = "USED" + Guid.NewGuid().ToString("N")[..8],
            PermissionKeys = [Permissions.PurchasesView],
        });
        var roleResult = await createdRole.Content.ReadFromJsonAsync<ApiResponse<RoleDetailDto>>(_jsonOptions);
        await CreateUserAsync(new CreateUserRequest
        {
            Username = UniqueUsername(),
            DisplayName = "占位用户",
            Password = "usedrole123",
            RoleIds = [Guid.Parse(roleResult!.Data!.Id)],
        });

        var response = await _client.DeleteAsync($"/api/roles/{roleResult.Data.Id}");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object?>>(_jsonOptions);

        Assert.Equal(ErrorCode.RoleInUse, result!.Code);
    }

    [Fact]
    public async Task 无权限角色_访问用户列表_返回HTTP200且code40300()
    {
        await LoginAsAdminAsync();

        // 新建一个只有 staff 查看权限的角色，用它创建一个新用户
        var roleResponse = await _client.PostAsJsonAsync("/api/roles", new CreateRoleRequest
        {
            Name = "NOROLE" + Guid.NewGuid().ToString("N")[..8],
            PermissionKeys = [Permissions.PurchasesView],
        });
        var roleResult = await roleResponse.Content.ReadFromJsonAsync<ApiResponse<RoleDetailDto>>(_jsonOptions);
        var username = UniqueUsername();
        await _client.PostAsJsonAsync("/api/users", new CreateUserRequest
        {
            Username = username,
            DisplayName = "受限用户",
            Password = "limited123",
            RoleIds = [Guid.Parse(roleResult!.Data!.Id)],
        });

        // 以受限用户登录
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Username = username, Password = "limited123" });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(_jsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult!.Data!.Token);

        // 无权限：HTTP 200 + code 40300（不返回 401 / 403）
        var response = await _client.GetAsync("/api/users?keyword=");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<UserListItemDto>>>(_jsonOptions);
        Assert.Equal(ErrorCode.Forbidden, result!.Code);

        // 仍可访问白名单接口（未返回 40300）
        var whitelist = await GetAsync<IReadOnlyList<string>>("/api/users/me/permissions");
        Assert.Equal(0, whitelist.Code);
        Assert.Equal([Permissions.PurchasesView], whitelist.Data!);
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
            RoleIds = [_staffRoleId],
        });
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserDetailDto>>(_jsonOptions);

        Assert.Equal(0, result!.Code);
        Assert.Equal(username, result.Data!.Username);
        Assert.Equal("已改名", result.Data.DisplayName);
        Assert.NotNull(result.Data.Email);
        Assert.Single(result.Data.Roles);
    }

    [Fact]
    public async Task 编辑用户_用户不存在_返回40400()
    {
        await LoginAsAdminAsync();

        var response = await _client.PutAsJsonAsync(
            $"/api/users/{Guid.NewGuid()}",
            new UpdateUserRequest { DisplayName = "任意", RoleIds = [_staffRoleId] });
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

    [Fact]
    public async Task 导出商品_成功_返回xlsx文件流()
    {
        await LoginAsAdminAsync();

        var response = await _client.GetAsync("/api/products/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // 契约例外：成功返回 xlsx 二进制流（非统一响应 JSON）
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("xlsx", response.Content.Headers.ContentDisposition!.FileNameStar ?? string.Empty, StringComparison.Ordinal);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.NotNull(workbook.Worksheet("商品"));
    }

    [Fact]
    public async Task 导出商品_分页参数越界_返回统一响应JSON()
    {
        await LoginAsAdminAsync();

        var result = await _client.GetFromJsonAsync<ApiResponse<JsonElement>>(
            "/api/products/export?pageSize=1000",
            _jsonOptions);

        // 错误回退：参数非法时仍返回统一响应 JSON（前端按 Content-Type 区分）
        Assert.Equal(40000, result!.Code);
    }
}
