using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.ServiceTickets.AssignServiceTicket;

/// <summary>
/// 指派服务工单负责人用例（design.md §3.3 / §3.4）：
/// 取工单（40400）→ 已关闭拒绝指派（40172）→ 负责人存在性（40400）→ 回写负责人 → 记操作日志（动作 Update）。
/// </summary>
public sealed class AssignServiceTicketRequestHandler : IRequestHandler<AssignServiceTicketRequest, ServiceTicketDetailDto>
{
    private readonly IServiceTicketRepository _ticketRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化指派服务工单负责人用例处理器
    /// </summary>
    public AssignServiceTicketRequestHandler(
        IServiceTicketRepository ticketRepository,
        IEmployeeRepository employeeRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _ticketRepository = ticketRepository;
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理指派服务工单负责人请求
    /// </summary>
    /// <param name="request">指派请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ServiceTicketDetailDto> HandleAsync(AssignServiceTicketRequest request, CancellationToken cancellationToken = default)
    {
        var detail = await _ticketRepository.GetByIdAsync(request.Id, cancellationToken);
        if (detail is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "工单不存在");
        }

        // 已关闭为终态（归档）：不可指派
        ServiceTicketStatusRules.EnsureNotClosed(detail.Ticket.Status);

        var owner = await _employeeRepository.GetByIdAsync(request.OwnerId, cancellationToken);
        if (owner is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "负责人不存在");
        }

        var ticket = detail.Ticket;
        var oldOwnerName = detail.OwnerName;
        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        ticket.OwnerId = owner.Id;
        ticket.UpdatedAt = now;
        ticket.UpdatedBy = operatorId;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _ticketRepository.UpdateAsync(ticket, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("ownerName", "负责人", oldOwnerName, owner.Name);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.ServiceTicket,
                Action = AuditAction.Update,
                ResourceId = ticket.Id,
                ResourceNo = ticket.TicketNo,
                Summary = $"指派服务工单 {ticket.TicketNo}（负责人：{owner.Name}）",
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
