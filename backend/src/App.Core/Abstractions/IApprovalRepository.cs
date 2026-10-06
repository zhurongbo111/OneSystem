using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 审批记录仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL，specs/042-erp-approval/design.md §3.2）。
/// 一张单据一条记录（唯一索引 <c>(OrderType, OrderId)</c>）；记录只写入状态流转，不删除。
/// </summary>
public interface IApprovalRepository
{
    /// <summary>
    /// 新增审批记录并持久化（与单据落库同一事务，由 Handler 用 IUnitOfWork 包裹）
    /// </summary>
    /// <param name="approval">审批记录实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(Approval approval, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分页查询审批记录：状态 / 单据类型 / 提交人 / 提交时间闭区间筛选，提交时间倒序。
    /// 「待我审批」= <paramref name="status"/> 传 <see cref="ApprovalStatus.Pending"/>
    /// </summary>
    /// <param name="status">审批状态，可空（不传 = 全部）</param>
    /// <param name="orderType">单据类型，可空</param>
    /// <param name="submittedBy">提交人 id，可空</param>
    /// <param name="start">起始提交时间（含），可空</param>
    /// <param name="end">结束提交时间（含），可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<Approval> Items, int Total)> GetPagedAsync(
        ApprovalStatus? status,
        SettlementOrderType? orderType,
        Guid? submittedBy,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 id 查询审批记录（详情 / 审批动作），不存在返回 null
    /// </summary>
    /// <param name="id">审批记录 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<Approval?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按单据取审批记录（单据列表 / 详情展示审批信息），不存在返回 null
    /// </summary>
    /// <param name="orderType">单据类型</param>
    /// <param name="orderId">被审批单据 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<Approval?> GetByOrderAsync(SettlementOrderType orderType, Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 写入审批决定（通过 / 驳回 / 撤回）：原子更新状态、审批人、审批时间与意见
    /// </summary>
    /// <param name="id">审批记录 id</param>
    /// <param name="status">目标状态（Approved / Rejected / Withdrawn）</param>
    /// <param name="decidedBy">审批人 / 撤回人 id</param>
    /// <param name="decidedAt">决定时间</param>
    /// <param name="remark">审批意见（可空）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateDecisionAsync(
        Guid id,
        ApprovalStatus status,
        Guid decidedBy,
        DateTimeOffset decidedAt,
        string? remark,
        CancellationToken cancellationToken = default);
}
