using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Approvals.UpdateApprovalRules;

/// <summary>
/// 审批规则保存用例（specs/042-erp-approval/design.md §3.5）：逐类型 upsert（阈值 + 启用开关），
/// 同一事务内写操作日志（规则无独立实体 id，审计按「单据类型 → 规则」摘要记录）；提交后再回读最新规则。
/// 规则改了即覆盖，不保留历史版本（变更留痕由 `029` 承担）。
/// </summary>
public sealed class UpdateApprovalRulesRequestHandler : IRequestHandler<UpdateApprovalRulesRequest, IReadOnlyList<ApprovalRuleDto>>
{
    private static readonly SettlementOrderType[] _orderTypes =
    [
        SettlementOrderType.PurchaseInbound,
        SettlementOrderType.SalesOutbound,
        SettlementOrderType.PurchaseReturn,
        SettlementOrderType.SalesReturn,
    ];

    private readonly IApprovalRuleRepository _approvalRuleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化审批规则保存用例处理器
    /// </summary>
    public UpdateApprovalRulesRequestHandler(
        IApprovalRuleRepository approvalRuleRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _approvalRuleRepository = approvalRuleRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理审批规则保存请求
    /// </summary>
    /// <param name="request">保存请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IReadOnlyList<ApprovalRuleDto>> HandleAsync(
        UpdateApprovalRulesRequest request, CancellationToken cancellationToken = default)
    {
        var operatorId = _currentUser.UserId()
            ?? throw new BusinessException(ErrorCode.Unauthorized, "登录状态无效，请重新登录");
        var now = DateTimeOffset.UtcNow;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var rule in request.Rules)
            {
                await _approvalRuleRepository.UpsertAsync(
                    rule.OrderType, rule.ThresholdAmount, rule.Enabled, operatorId, now, cancellationToken);
            }

            // 业务写成功后、提交前追加操作日志：与业务同事务，异常回滚则不产生日志
            var summary = string.Join("；", request.Rules.Select(rule =>
                $"{AuditText.SettlementOrderType(rule.OrderType)}：{(rule.Enabled ? $"≥ {AuditSummary.Money(rule.ThresholdAmount)} 需审批" : "保存即生效")}"));
            var changeBuilder = new AuditChangeBuilder().Add("rules", "审批规则", null, summary);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Approval,
                Action = AuditAction.Update,
                ResourceId = null,
                ResourceNo = "approval-rules",
                Summary = $"更新审批规则（{summary}）",
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

        // 提交后回读：返回四类规则的最新值（缺失类型仍返回默认未启用行）
        var rules = await _approvalRuleRepository.GetAllAsync(cancellationToken);
        var byType = rules.ToDictionary(r => r.OrderType);
        return _orderTypes
            .Select(orderType => ApprovalsDtoMapper.ToApprovalRuleDto(
                orderType, byType.TryGetValue(orderType, out var rule) ? rule : null))
            .ToList();
    }
}
