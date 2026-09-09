using App.Core.Auth;
using App.Core.Dtos;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace App.Tests;

/// <summary>
/// JWT 签发与校验测试
/// </summary>
public class TokenServiceTests
{
    private static JwtOptions CreateOptions(string? secret = null)
        => new() { Secret = secret ?? new string('a', 48), Issuer = "app-api", Audience = "app-web", ExpiresMinutes = 120 };

    private static TokenService CreateService(JwtOptions options)
        => new(Options.Create(options), NullLogger<TokenService>.Instance);

    private static UserDto CreateUser() => new() { Id = "1", Username = "admin", DisplayName = "管理员" };

    [Fact]
    public void Issue_再校验_应通过且还原用户()
    {
        var service = CreateService(CreateOptions());

        var token = service.Issue(CreateUser());
        var principal = service.Validate(token);

        Assert.NotNull(principal);
        Assert.Equal("admin", principal!.FindFirst("username")?.Value);
        Assert.Equal("管理员", principal.FindFirst("displayName")?.Value);
        // 默认入站映射：sub 会映射为 ClaimTypes.NameIdentifier
        Assert.Equal("1", principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
    }

    [Fact]
    public void Validate_无效token_应返回null()
    {
        var service = CreateService(CreateOptions());

        Assert.Null(service.Validate("not.a.jwt"));
    }

    [Fact]
    public void Validate_错误密钥签发_应返回null()
    {
        var issuer = CreateService(CreateOptions(secret: new string('a', 48)));
        var other = CreateService(CreateOptions(secret: new string('b', 48)));

        Assert.Null(other.Validate(issuer.Issue(CreateUser())));
    }
}
