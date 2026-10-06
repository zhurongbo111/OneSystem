using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 审批规则仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL，specs/042-erp-approval/design.md §3.2）。
/// 规则按**单据类型**一条（唯一索引）；规则默认不存在 = 该类单据不启用审批（升级后行为逐条不变）。
/// </summary>
public interface IApprovalRuleRepository
{
    /// <summary>
    /// 取全部规则（规则维护页展示用；缺失的类型由 Handler 补默认「未启用」行）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<ApprovalRule>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取某单据类型的规则（创建单据时判定是否触发审批），不存在返回 null（视为未启用）
    /// </summary>
    /// <param name="orderType">单据类型</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<ApprovalRule?> GetAsync(SettlementOrderType orderType, CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存某单据类型的规则（不存在则新建、存在则覆盖阈值与启用开关）并持久化
    /// </summary>
    /// <param name="orderType">单据类型</param>
    /// <param name="thresholdAmount">审批阈值（&gt; 0）</param>
    /// <param name="enabled">是否启用</param>
    /// <param name="operatorId">操作人 id（由 Handler 取 ICurrentUser 传入，可空）</param>
    /// <param name="utcNow">操作时间（由 Handler 注入，便于单测）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpsertAsync(
        SettlementOrderType orderType,
        decimal thresholdAmount,
        bool enabled,
        Guid? operatorId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);
}
