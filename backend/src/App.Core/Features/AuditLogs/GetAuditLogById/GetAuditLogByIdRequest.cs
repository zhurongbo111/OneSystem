using App.Core.Abstractions;

namespace App.Core.Features.AuditLogs.GetAuditLogById;

/// <summary>
/// 操作日志详情请求（路由参数 id）
/// </summary>
public sealed class GetAuditLogByIdRequest : IRequest<AuditLogDetailDto>
{
    /// <summary>日志 id</summary>
    public Guid Id { get; init; }
}
