using App.Core.Auth;
using App.Core.Dtos;
using App.Core.Errors;
using App.Core.Handlers;
using App.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace App.Tests;

/// <summary>
/// 认证 Handler 测试
/// </summary>
public class AuthHandlerTests
{
    private static AuthHandler CreateHandler()
    {
        var options = new JwtOptions { Secret = new string('a', 48), Issuer = "app-api", Audience = "app-web", ExpiresMinutes = 120 };
        var tokenService = new TokenService(Options.Create(options), NullLogger<TokenService>.Instance);
        return new AuthHandler(new InMemoryUserAccountService(), tokenService);
    }

    [Fact]
    public async Task LoginAsync_正确账号_应返回token与用户()
    {
        var handler = CreateHandler();

        var result = await handler.LoginAsync(new LoginRequest { Username = "admin", Password = "admin123" });

        Assert.False(string.IsNullOrEmpty(result.Token));
        Assert.Equal("admin", result.User.Username);
        Assert.Equal("管理员", result.User.DisplayName);
    }

    [Fact]
    public async Task LoginAsync_错误密码_应抛业务异常40001()
    {
        var handler = CreateHandler();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.LoginAsync(new LoginRequest { Username = "admin", Password = "wrong" }));

        Assert.Equal(ErrorCode.LoginFailed, ex.Code);
    }

    [Fact]
    public async Task LoginAsync_参数为空_应抛业务异常40000()
    {
        var handler = CreateHandler();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.LoginAsync(new LoginRequest { Username = " ", Password = "" }));

        Assert.Equal(ErrorCode.Validation, ex.Code);
    }
}
