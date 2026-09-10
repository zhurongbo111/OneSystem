using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Api.Http;

/// <summary>
/// 客户端信息实现：从 HttpContext 读取 IP 与 User-Agent，供需要记录登录来源的用例使用。
/// 与 CurrentUserAccessor 同理，Handler 不直接接触 HTTP 上下文，便于单测注入桩对象。
/// </summary>
internal sealed class ClientInfoAccessor : IClientInfo
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// 初始化客户端信息访问器
    /// </summary>
    public ClientInfoAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// 客户端 IP：取连接远程地址；无 HTTP 上下文或取不到时为空。
    /// 反向代理（X-Forwarded-For）配置受信代理后再统一处理，避免采信可伪造的头部。
    /// </summary>
    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    /// <summary>客户端 User-Agent：空串按空值处理，超长截断</summary>
    public string? UserAgent
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= UserFieldConstraints.UserAgentMaxLength
                ? trimmed
                : trimmed[..UserFieldConstraints.UserAgentMaxLength];
        }
    }
}
