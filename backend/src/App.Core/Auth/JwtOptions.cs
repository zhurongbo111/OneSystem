namespace App.Core.Auth;

/// <summary>
/// JWT 配置：敏感项（Secret）从环境变量注入，禁止硬编码
/// </summary>
public class JwtOptions
{
    /// <summary>配置节名称</summary>
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 签名密钥（≥32 字符），环境变量 JWT__SECRET 注入</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>签发者</summary>
    public string Issuer { get; set; } = "app-api";

    /// <summary>受众</summary>
    public string Audience { get; set; } = "app-web";

    /// <summary>token 有效期（分钟）</summary>
    public int ExpiresMinutes { get; set; } = 120;
}
