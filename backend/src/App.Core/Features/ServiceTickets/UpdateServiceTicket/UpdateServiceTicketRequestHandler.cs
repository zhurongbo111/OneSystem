using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.ServiceTickets.UpdateServiceTicket;

/// <summary>
/// 编辑服务工单用例（design.md §3.3 / §3.4，**全量覆盖**语义）：
/// 取工单（40400）→ 已关闭拒绝（40172）→ 客户 / 负责人存在性（40400）→ 更新字段（含客户名快照）
/// → 记操作日志 → 提交。**不触碰库存、库存流水与收付款**；状态不在本用例可改范围内。
/// </summary>
public sealed class UpdateServiceTicketRequestHandler : IRequestHandler<UpdateServiceTicketRequest, ServiceTicketDetailDto>
{
    private readonly IServiceTicketRepository _ticketRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化编辑服务工单用例处理器
    /// </summary>
    public UpdateServiceTicketRequestHandler(
        IServiceTicketRepository ticketRepository,
        IPartnerRepository partnerRepository,
        IEmployeeRepository employeeRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _ticketRepository = ticketRepository;
        _partnerRepository = partnerRepository;
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理编辑服务工单请求
    /// </summary>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ServiceTicketDetailDto> HandleAsync(UpdateServiceTicketRequest request, CancellationToken cancellationToken = default)
    {
        var detail = await _ticketRepository.GetByIdAsync(request.Id, cancellationToken);
        if (detail is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "工单不存在");
        }

        // 已关闭为终态（归档）：不可编辑
        ServiceTicketStatusRules.EnsureNotClosed(detail.Ticket.Status);

        var partner = await _partnerRepository.GetByIdAsync(request.PartnerId, cancellationToken);
        if (partner is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "客户不存在");
        }

        if (request.OwnerId is not null)
        {
            var owner = await _employeeRepository.GetByIdAsync(request.OwnerId.Value, cancellationToken);
            if (owner is null)
            {
                throw new BusinessException(ErrorCode.NotFound, "负责人不存在");
            }
        }

        var ticket = detail.Ticket;
        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        // 变更前后的旧值（日志用）
        var oldPartnerName = ticket.PartnerName;
        var oldContact = ticket.Contact;
        var oldPhone = ticket.Phone;
        var oldTitle = ticket.Title;
        var oldDescription = ticket.Description;
        var oldPriority = ticket.Priority;
        var oldRemark = ticket.Remark;

        ticket.PartnerId = partner.Id;
        ticket.PartnerName = partner.Name;
        ticket.Contact = Normalize(request.Contact);
        ticket.Phone = Normalize(request.Phone);
        ticket.Title = request.Title.Trim();
        ticket.Description = Normalize(request.Description);
        ticket.Priority = request.Priority;
        ticket.OwnerId = request.OwnerId;
        ticket.Remark = Normalize(request.Remark);
        ticket.UpdatedAt = now;
        ticket.UpdatedBy = operatorId;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _ticketRepository.UpdateAsync(ticket, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("partnerName", "客户", oldPartnerName, ticket.PartnerName)
                .Add("title", "工单标题", oldTitle, ticket.Title)
                .Add("contact", "联系人", oldContact, ticket.Contact)
                .Add("phone", "联系电话", oldPhone, ticket.Phone)
                .Add("description", "问题描述", oldDescription, ticket.Description)
                .Add("priority", "优先级", AuditText.TicketPriority(oldPriority), AuditText.TicketPriority(ticket.Priority))
                .Add("remark", "备注", oldRemark, ticket.Remark);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.ServiceTicket,
                Action = AuditAction.Update,
                ResourceId = ticket.Id,
                ResourceNo = ticket.TicketNo,
                Summary = $"编辑服务工单 {ticket.TicketNo}（{ticket.PartnerName}、{ticket.Title}）",
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

    /// <summary>可空文本归一：空白视为清空（落库 null），有值则 Trim（AGENTS.md §4.5）</summary>
    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
