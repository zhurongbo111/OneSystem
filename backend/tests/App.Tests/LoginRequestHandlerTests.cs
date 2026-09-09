using App.Core.Auth;
using App.Core.Errors;
using App.Core.Features.Auth.Login;
using App.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace App.Tests;

/// <summary>
/// 登录用例处理器测试
/// </summary>
public class LoginRequestHandlerTests
{
    private static LoginRequestHandler CreateHandler()
    {
        var options = new JwtOptions { Secret = new string('a', 48), Issuer = "app-api", Audience = "app-web", ExpiresMinutes = 120 };
        var tokenService = new TokenService(Options.Create(options), NullLogger<TokenService>.Instance);
        // 格式校验由 Mediator 分发前统一执行，处理器不再依赖校验器
        return new LoginRequestHandler(new InMemoryUserRepository(), tokenService);
    }

    [Fact]
    public async Task HandleAsync_正确账号_应返回token与用户()
    {
        var handler = CreateHandler();

        var result = await handler.HandleAsync(new LoginRequest { Username = "admin", Password = "admin123" });

        Assert.False(string.IsNullOrEmpty(result.Token));
        Assert.Equal("admin", result.User.Username);
        Assert.Equal("管理员", result.User.DisplayName);
    }

    [Fact]
    public async Task HandleAsync_错误密码_应抛业务异常40001()
    {
        var handler = CreateHandler();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new LoginRequest { Username = "admin", Password = "wrong" }));

        Assert.Equal(ErrorCode.LoginFailed, ex.Code);
    }

    [Fact]
    public async Task HandleAsync_用户不存在_应抛业务异常40001()
    {
        var handler = CreateHandler();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new LoginRequest { Username = "nobody", Password = "admin123" }));

        Assert.Equal(ErrorCode.LoginFailed, ex.Code);
    }

    // 格式校验（空参数 40000）已由 Mediator 全局统一执行，用例见 MediatorTests
}
