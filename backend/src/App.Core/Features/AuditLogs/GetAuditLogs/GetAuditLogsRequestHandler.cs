using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.AuditLogs.GetAuditLogs;

/// <summary>
/// 操作日志分页查询用例：关键词 / 资源类型 / 动作 / 操作人 / 时间范围筛选，操作时间倒序（只读）
/// </summary>
public sealed class GetAuditLogsRequestHandler : IRequestHandler<GetAuditLogsRequest, PagedResult<AuditLogListItemDto>>
{
    private readonly IAuditLogRepository _auditLogRepository;

    /// <summary>
    /// 初始化操作日志查询用例处理器
    /// </summary>
    public GetAuditLogsRequestHandler(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    /// <summary>
    /// 处理操作日志查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<AuditLogListItemDto>> HandleAsync(GetAuditLogsRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _auditLogRepository.GetPagedAsync(
            request.Keyword,
            request.Resource is null ? null : (AuditResource)request.Resource.Value,
            request.Action is null ? null : (AuditAction)request.Action.Value,
            request.UserId,
            request.Start,
            request.End,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResult<AuditLogListItemDto>
        {
            Items = items.Select(AuditLogsDtoMapper.ToAuditLogListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
