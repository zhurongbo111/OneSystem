using System.IdentityModel.Tokens.Jwt;
using App.Core.Auth;
using App.Core.Features.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace App.Tests;

/// <summary>
/// JWT 签发测试（token 无效 / 错误密钥等校验语义已由 ApiIntegrationTests 的 40100 用例覆盖）
/// </summary>
public class TokenServiceTests
{
    private static JwtOptions CreateOptions(string? secret = null)
        => new() { Secret = secret ?? new string('a', 48), Issuer = "app-api", Audience = "app-web", ExpiresMinutes = 120 };

    private static TokenService CreateService(JwtOptions options)
        => new(Options.Create(options), NullLogger<TokenService>.Instance);

    private static UserDto CreateUser() => new() { Id = "1", Username = "admin", DisplayName = "管理员" };

    [Fact]
    public void Issue_应签发含用户claims与预期issuerAudience的token()
    {
        var service = CreateService(CreateOptions());

        var token = service.Issue(CreateUser());

        Assert.False(string.IsNullOrEmpty(token));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("app-api", jwt.Issuer);
        Assert.Equal("app-web", Assert.Single(jwt.Audiences));
        Assert.Equal("1", jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("admin", jwt.Claims.First(c => c.Type == "username").Value);
        Assert.Equal("管理员", jwt.Claims.First(c => c.Type == "displayName").Value);
    }

    [Fact]
    public void Issue_应设置与配置一致的有效期()
    {
        var service = CreateService(CreateOptions());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(service.Issue(CreateUser()));

        Assert.True(jwt.ValidFrom <= DateTime.UtcNow);
        Assert.InRange(jwt.ValidTo, DateTime.UtcNow.AddMinutes(119), DateTime.UtcNow.AddMinutes(121));
    }
}
