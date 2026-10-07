using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.ServiceTickets.UpdateServiceTicketStatus;

/// <summary>
/// 服务工单状态流转用例（design.md §0.1 / §3.3 / §3.4）：
/// 取工单（40400）→ 白名单流转校验（非法组合 / 已关闭终态 40172）→ 更新状态
/// → 置「已解决」记 <c>ResolvedAt</c>、「已解决 → 处理中」重开清空 → 记操作日志。
/// </summary>
public sealed class UpdateServiceTicketStatusRequestHandler : IRequestHandler<UpdateServiceTicketStatusRequest, ServiceTicketDetailDto>
{
    private readonly IServiceTicketRepository _ticketRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化服务工单状态流转用例处理器
    /// </summary>
    public UpdateServiceTicketStatusRequestHandler(
        IServiceTicketRepository ticketRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _ticketRepository = ticketRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理服务工单状态流转请求
    /// </summary>
    /// <param name="request">状态流转请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ServiceTicketDetailDto> HandleAsync(UpdateServiceTicketStatusRequest request, CancellationToken cancellationToken = default)
    {
        var detail = await _ticketRepository.GetByIdAsync(request.Id, cancellationToken);
        if (detail is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "工单不存在");
        }

        var ticket = detail.Ticket;
        var oldStatus = ticket.Status;
        ServiceTicketStatusRules.EnsureTransitionAllowed(oldStatus, request.Status);

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        ticket.Status = request.Status;
        // 置「已解决」记录解决时间；重开（已解决 → 处理中）清空（design.md §0.1）
        if (request.Status == TicketStatus.Resolved)
        {
            ticket.ResolvedAt = now;
        }
        else if (oldStatus == TicketStatus.Resolved && request.Status == TicketStatus.Processing)
        {
            ticket.ResolvedAt = null;
        }

        ticket.UpdatedAt = now;
        ticket.UpdatedBy = operatorId;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _ticketRepository.UpdateAsync(ticket, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("status", "状态", AuditText.TicketStatus(oldStatus), AuditText.TicketStatus(ticket.Status));
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.ServiceTicket,
                Action = AuditAction.StatusChange,
                ResourceId = ticket.Id,
                ResourceNo = ticket.TicketNo,
                Summary = $"服务工单 {ticket.TicketNo} 状态变更为{AuditText.TicketStatus(ticket.Status)}",
                Changes = changeBuilder.Build(),
                ChangesTruncated = changeBuilder.Truncated,
                UtcNow = now,
            }, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        var updated = await _ticketRepository.GetByIdAsync(ticket.Id, cancellationToken);
        if (updated is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "工单更新后读取失败");
        }

        return ServiceTicketsDtoMapper.ToServiceTicketDetailDto(updated);
    }
}
