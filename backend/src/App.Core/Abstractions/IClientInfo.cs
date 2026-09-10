namespace App.Core.Abstractions;

/// <summary>
/// 客户端信息（由 App.Api 基于 HttpContext 实现）。
/// 需要记录登录来源（IP / User-Agent）的 RequestHandler 依赖本接口，避免直接接触 HTTP 上下文。
/// </summary>
public interface IClientInfo
{
    /// <summary>客户端 IP；无 HTTP 上下文或取不到时为空</summary>
    string? IpAddress { get; }

    /// <summary>客户端 User-Agent；无 HTTP 上下文或取不到时为空</summary>
    string? UserAgent { get; }
}
