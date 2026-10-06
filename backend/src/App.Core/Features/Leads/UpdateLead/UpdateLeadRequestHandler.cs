using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Leads.UpdateLead;

/// <summary>
/// 编辑线索用例（design.md §3.3，**全量覆盖**语义）：
/// 取线索（40400）→ 负责人存在性校验（40400）→ 状态流转规则（<see cref="LeadStatusRules"/>）
/// → 更新字段 → 记操作日志 → 提交。**不触碰库存、库存流水与收付款**。
/// </summary>
public sealed class UpdateLeadRequestHandler : IRequestHandler<UpdateLeadRequest, LeadDetailDto>
{
    private readonly ILeadRepository _leadRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化编辑线索用例处理器
    /// </summary>
    public UpdateLeadRequestHandler(
        ILeadRepository leadRepository,
        IEmployeeRepository employeeRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _leadRepository = leadRepository;
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理编辑线索请求
    /// </summary>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<LeadDetailDto> HandleAsync(UpdateLeadRequest request, CancellationToken cancellationToken = default)
    {
        var detail = await _leadRepository.GetByIdAsync(request.Id, cancellationToken);
        if (detail is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "线索不存在");
        }

        if (request.OwnerId is not null)
        {
            var owner = await _employeeRepository.GetByIdAsync(request.OwnerId.Value, cancellationToken);
            if (owner is null)
            {
                throw new BusinessException(ErrorCode.NotFound, "负责人不存在");
            }
        }

        var lead = detail.Lead;
        if (request.Status != lead.Status)
        {
            LeadStatusRules.EnsureChangeAllowed(lead.Status, request.Status);
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        // 变更前后的旧值（日志用）
        var oldName = lead.Name;
        var oldContact = lead.Contact;
        var oldPhone = lead.Phone;
        var oldSource = lead.Source;
        var oldStatus = lead.Status;
        var oldRemark = lead.Remark;

        lead.Name = request.Name.Trim();
        lead.Contact = Normalize(request.Contact);
        lead.Phone = Normalize(request.Phone);
        lead.Source = request.Source;
        lead.Status = request.Status;
        lead.OwnerId = request.OwnerId;
        lead.Remark = Normalize(request.Remark);
        lead.UpdatedAt = now;
        lead.UpdatedBy = operatorId;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _leadRepository.UpdateAsync(lead, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("name", "线索名称", oldName, lead.Name)
                .Add("contact", "联系人", oldContact, lead.Contact)
                .Add("phone", "联系电话", oldPhone, lead.Phone)
                .Add("source", "来源", AuditText.LeadSource(oldSource), AuditText.LeadSource(lead.Source))
                .Add("status", "状态", AuditText.LeadStatus(oldStatus), AuditText.LeadStatus(lead.Status))
                .Add("remark", "备注", oldRemark, lead.Remark);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Lead,
                Action = AuditAction.Update,
                ResourceId = lead.Id,
                ResourceNo = lead.LeadNo,
                Summary = $"编辑线索 {lead.LeadNo}（{lead.Name}）",
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

        var updated = await _leadRepository.GetByIdAsync(lead.Id, cancellationToken);
        if (updated is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "线索更新后读取失败");
        }

        return LeadsDtoMapper.ToLeadDetailDto(updated);
    }

    /// <summary>可空文本归一：空白视为清空（落库 null），有值则 Trim（AGENTS.md §4.5）</summary>
    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
