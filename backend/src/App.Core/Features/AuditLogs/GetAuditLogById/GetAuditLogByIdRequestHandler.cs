using App.Core.Abstractions;
using App.Core.Errors;

using Microsoft.Extensions.Logging;

namespace App.Core.Features.AuditLogs.GetAuditLogById;

/// <summary>
/// 操作日志详情用例：按 id 查询单条日志（含结构化字段差异），不存在抛 40400
/// </summary>
public sealed class GetAuditLogByIdRequestHandler : IRequestHandler<GetAuditLogByIdRequest, AuditLogDetailDto>
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<GetAuditLogByIdRequestHandler> _logger;

    /// <summary>
    /// 初始化操作日志详情用例处理器
    /// </summary>
    public GetAuditLogByIdRequestHandler(IAuditLogRepository auditLogRepository, ILogger<GetAuditLogByIdRequestHandler> logger)
    {
        _auditLogRepository = auditLogRepository;
        _logger = logger;
    }

    /// <summary>
    /// 处理操作日志详情请求
    /// </summary>
    /// <param name="request">详情请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<AuditLogDetailDto> HandleAsync(GetAuditLogByIdRequest request, CancellationToken cancellationToken = default)
    {
        var log = await _auditLogRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "操作日志不存在");

        return AuditLogsDtoMapper.ToAuditLogDetailDto(log, _logger);
    }
}
