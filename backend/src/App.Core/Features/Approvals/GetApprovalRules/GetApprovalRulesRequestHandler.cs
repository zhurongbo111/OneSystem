using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Approvals.GetApprovalRules;

/// <summary>
/// 审批规则查询用例：按固定顺序返回四类单据规则（采购入库 / 销售出库 / 采购退货 / 销售退货），
/// 未配置的类型返回默认「未启用 / 阈值 0」行（规则维护页始终展示四行）。
/// </summary>
public sealed class GetApprovalRulesRequestHandler : IRequestHandler<GetApprovalRulesRequest, IReadOnlyList<ApprovalRuleDto>>
{
    private static readonly SettlementOrderType[] _orderTypes =
    [
        SettlementOrderType.PurchaseInbound,
        SettlementOrderType.SalesOutbound,
        SettlementOrderType.PurchaseReturn,
        SettlementOrderType.SalesReturn,
    ];

    private readonly IApprovalRuleRepository _approvalRuleRepository;

    /// <summary>
    /// 初始化审批规则查询用例处理器
    /// </summary>
    public GetApprovalRulesRequestHandler(IApprovalRuleRepository approvalRuleRepository)
    {
        _approvalRuleRepository = approvalRuleRepository;
    }

    /// <summary>
    /// 处理审批规则查询请求
    /// </summary>
    /// <param name="request">空请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IReadOnlyList<ApprovalRuleDto>> HandleAsync(
        GetApprovalRulesRequest request, CancellationToken cancellationToken = default)
    {
        var rules = await _approvalRuleRepository.GetAllAsync(cancellationToken);
        var byType = rules.ToDictionary(r => r.OrderType);

        return _orderTypes
            .Select(orderType => ApprovalsDtoMapper.ToApprovalRuleDto(
                orderType, byType.TryGetValue(orderType, out var rule) ? rule : null))
            .ToList();
    }
}
