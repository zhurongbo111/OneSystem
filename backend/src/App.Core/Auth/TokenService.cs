using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using App.Core.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace App.Core.Auth;

/// <summary>
/// JWT 签发服务
/// </summary>
public class TokenService
{
    private readonly JwtOptions _options;
    private readonly byte[] _key;
    private readonly ILogger<TokenService> _logger;

    /// <summary>
    /// 初始化签发服务
    /// </summary>
    /// <param name="options">JWT 配置</param>
    /// <param name="logger">日志</param>
    public TokenService(IOptions<JwtOptions> options, ILogger<TokenService> logger)
    {
        _options = options.Value;
        _key = Encoding.UTF8.GetBytes(_options.Secret);
        _logger = logger;
        if (string.IsNullOrEmpty(_options.Issuer) || string.IsNullOrEmpty(_options.Audience))
        {
            throw new InvalidOperationException("JWT 配置缺失：Issuer / Audience 不能为空");
        }

        if (_options.Secret.Length < 32)
        {
            _logger.LogWarning("JWT 签名密钥长度不足 32 字符（当前 {Length}），traceId={TraceId}", _options.Secret.Length, Activity.Current?.TraceId);
        }
    }

    /// <summary>
    /// 为指定用户签发 JWT
    /// </summary>
    /// <param name="user">用户信息</param>
    /// <returns>JWT token 字符串</returns>
    public string Issue(UserDto user)
    {
        var now = DateTime.UtcNow;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim("username", user.Username),
            new Claim("displayName", user.DisplayName),
            new Claim(JwtRegisteredClaimNames.Iss, _options.Issuer),
            new Claim(JwtRegisteredClaimNames.Aud, _options.Audience),
        };
        var handler = new JwtSecurityTokenHandler();
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_options.ExpiresMinutes),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(_key), SecurityAlgorithms.HmacSha256));
        return handler.WriteToken(token);
    }

    /// <summary>
    /// 校验 JWT，成功时返回 ClaimsPrincipal，失败返回 null
    /// </summary>
    /// <param name="token">JWT token 字符串</param>
    public ClaimsPrincipal? Validate(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = true,
                ValidAudience = _options.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(_key),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
            }, out var validated);
            return validated is JwtSecurityToken ? principal : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 生成开发环境兜底随机密钥（仅 Development 环境使用）
    /// </summary>
    /// <returns>64 位十六进制随机字符串</returns>
    public static string GenerateDevSecret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}
