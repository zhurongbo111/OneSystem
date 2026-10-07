using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.ServiceTickets.CreateServiceTicket;

/// <summary>
/// 登记服务工单用例（design.md §3.3 / §3.4）：
/// 客户存在性（40400）→ 负责人存在性（40400）→ 单号生成（SV + yyyyMMdd + 序号，唯一索引冲突重试最多 3 次）
/// → 同一事务：插工单 + 记操作日志。**不触碰库存、库存流水与收付款**。
/// </summary>
public sealed class CreateServiceTicketRequestHandler : IRequestHandler<CreateServiceTicketRequest, ServiceTicketDetailDto>
{
    /// <summary>工单号前缀（见 design.md §0.2）</summary>
    private const string TicketNoPrefix = "SV";

    /// <summary>单号冲突重试上限（含首次）</summary>
    private const int MaxTicketNoAttempts = 3;

    private readonly IServiceTicketRepository _ticketRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化登记服务工单用例处理器
    /// </summary>
    public CreateServiceTicketRequestHandler(
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
    /// 处理登记服务工单请求
    /// </summary>
    /// <param name="request">登记请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ServiceTicketDetailDto> HandleAsync(CreateServiceTicketRequest request, CancellationToken cancellationToken = default)
    {
        // 查库约束：客户存在（名称快照取自客户主数据）
        var partner = await _partnerRepository.GetByIdAsync(request.PartnerId, cancellationToken);
        if (partner is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "客户不存在");
        }

        // 查库约束：负责人（员工）存在
        if (request.OwnerId is not null)
        {
            var owner = await _employeeRepository.GetByIdAsync(request.OwnerId.Value, cancellationToken);
            if (owner is null)
            {
                throw new BusinessException(ErrorCode.NotFound, "负责人不存在");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        // 事务：插工单 + 记日志；单号冲突（唯一索引）时回滚后重新生成单号重试
        for (var attempt = 1; ; attempt++)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var ticketNo = await _ticketRepository.GenerateNoAsync(TicketNoPrefix, now, cancellationToken);
                var ticket = new ServiceTicket
                {
                    Id = Guid.NewGuid(),
                    TicketNo = ticketNo,
                    PartnerId = partner.Id,
                    PartnerName = partner.Name,
                    Contact = Normalize(request.Contact),
                    Phone = Normalize(request.Phone),
                    Title = request.Title.Trim(),
                    Description = Normalize(request.Description),
                    Priority = request.Priority,
                    Status = TicketStatus.Pending,
                    OwnerId = request.OwnerId,
                    Remark = Normalize(request.Remark),
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = operatorId,
                    UpdatedBy = operatorId,
                };

                await _ticketRepository.AddAsync(ticket, cancellationToken);

                var changeBuilder = new AuditChangeBuilder()
                    .Add("ticketNo", "工单号", null, ticket.TicketNo)
                    .Add("partnerName", "客户", null, ticket.PartnerName)
                    .Add("title", "工单标题", null, ticket.Title)
                    .Add("contact", "联系人", null, ticket.Contact)
                    .Add("phone", "联系电话", null, ticket.Phone)
                    .Add("priority", "优先级", null, AuditText.TicketPriority(ticket.Priority))
                    .Add("status", "状态", null, AuditText.TicketStatus(ticket.Status))
                    .Add("remark", "备注", null, ticket.Remark);
                await _auditLogger.RecordAsync(new AuditEntry
                {
                    Resource = AuditResource.ServiceTicket,
                    Action = AuditAction.Create,
                    ResourceId = ticket.Id,
                    ResourceNo = ticket.TicketNo,
                    Summary = $"创建服务工单 {ticket.TicketNo}（{ticket.PartnerName}、{ticket.Title}）",
                    Changes = changeBuilder.Build(),
                    ChangesTruncated = changeBuilder.Truncated,
                    UtcNow = now,
                }, cancellationToken);

                await _unitOfWork.CommitAsync(cancellationToken);

                var created = await _ticketRepository.GetByIdAsync(ticket.Id, cancellationToken);
                if (created is null)
                {
                    throw new BusinessException(ErrorCode.NotFound, "工单创建后读取失败");
                }

                return ServiceTicketsDtoMapper.ToServiceTicketDetailDto(created);
            }
            catch (OrderNoConflictException) when (attempt < MaxTicketNoAttempts)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                continue;
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }

    /// <summary>可空文本归一：空白视为清空（落库 null），有值则 Trim（AGENTS.md §4.5）</summary>
    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
