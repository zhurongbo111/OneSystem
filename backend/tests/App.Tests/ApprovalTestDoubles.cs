using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Features.Approvals;

using Microsoft.Extensions.Logging.Abstractions;

namespace App.Tests;

/// <summary>
/// 审批规则仓储的行为型假实现（specs/042-erp-approval）：内存保存规则 + 记录 upsert 入参，
/// 规避 InMemory 不支持 <c>ExecuteUpdateAsync</c> 的关系型限制。
/// </summary>
internal sealed class FakeApprovalRuleRepository : IApprovalRuleRepository
{
    private readonly Dictionary<SettlementOrderType, ApprovalRule> _rules = [];

    /// <summary>已执行的 upsert 入参（orderType, threshold, enabled, operatorId）</summary>
    public List<(SettlementOrderType OrderType, decimal ThresholdAmount, bool Enabled, Guid? OperatorId)> Upserts { get; } = [];

    /// <summary>预置一条规则（供「命中阈值」用例）</summary>
    public void Seed(SettlementOrderType orderType, decimal thresholdAmount, bool enabled)
        => _rules[orderType] = new ApprovalRule
        {
            Id = Guid.NewGuid(),
            OrderType = orderType,
            ThresholdAmount = thresholdAmount,
            Enabled = enabled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    /// <inheritdoc />
    public Task<IReadOnlyList<ApprovalRule>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ApprovalRule>>(_rules.Values.OrderBy(r => r.OrderType).ToList());

    /// <inheritdoc />
    public Task<ApprovalRule?> GetAsync(SettlementOrderType orderType, CancellationToken cancellationToken = default)
        => Task.FromResult(_rules.GetValueOrDefault(orderType));

    /// <inheritdoc />
    public Task UpsertAsync(
        SettlementOrderType orderType,
        decimal thresholdAmount,
        bool enabled,
        Guid? operatorId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        Upserts.Add((orderType, thresholdAmount, enabled, operatorId));
        if (_rules.TryGetValue(orderType, out var existing))
        {
            existing.ThresholdAmount = thresholdAmount;
            existing.Enabled = enabled;
            existing.UpdatedAt = utcNow;
            existing.UpdatedBy = operatorId;
        }
        else
        {
            _rules[orderType] = new ApprovalRule
            {
                Id = Guid.NewGuid(),
                OrderType = orderType,
                ThresholdAmount = thresholdAmount,
                Enabled = enabled,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
                CreatedBy = operatorId,
                UpdatedBy = operatorId,
            };
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// 审批记录仓储的行为型假实现（specs/042-erp-approval）：内存保存记录 + 记录决定与分页入参。
/// </summary>
internal sealed class FakeApprovalRepository : IApprovalRepository
{
    private readonly Dictionary<Guid, Approval> _approvals = [];

    /// <summary>已存审批记录（供断言）</summary>
    public IReadOnlyCollection<Approval> Approvals => _approvals.Values;

    /// <summary>已执行的决定入参（status, decidedBy, decidedAt, remark）</summary>
    public List<(ApprovalStatus Status, Guid DecidedBy, DateTimeOffset DecidedAt, string? Remark)> Decisions { get; } = [];

    /// <summary>已执行的分页查询入参</summary>
    public List<(ApprovalStatus? Status, SettlementOrderType? OrderType, Guid? SubmittedBy,
        DateTimeOffset? Start, DateTimeOffset? End, int Page, int PageSize)> PagedQueries { get; } = [];

    /// <summary>分页查询返回的行（由用例预置）</summary>
    public IReadOnlyList<Approval> PagedItems { get; set; } = [];

    /// <summary>分页查询返回的总数（由用例预置）</summary>
    public int PagedTotal { get; set; }

    /// <summary>预置一条审批记录</summary>
    public void Seed(Approval approval) => _approvals[approval.Id] = approval;

    /// <inheritdoc />
    public Task AddAsync(Approval approval, CancellationToken cancellationToken = default)
    {
        _approvals[approval.Id] = approval;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<(IReadOnlyList<Approval> Items, int Total)> GetPagedAsync(
        ApprovalStatus? status,
        SettlementOrderType? orderType,
        Guid? submittedBy,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        PagedQueries.Add((status, orderType, submittedBy, start, end, page, pageSize));
        return Task.FromResult((PagedItems, PagedTotal));
    }

    /// <inheritdoc />
    public Task<Approval?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_approvals.GetValueOrDefault(id));

    /// <inheritdoc />
    public Task<Approval?> GetByOrderAsync(
        SettlementOrderType orderType, Guid orderId, CancellationToken cancellationToken = default)
        => Task.FromResult(_approvals.Values
            .FirstOrDefault(a => a.OrderType == orderType && a.OrderId == orderId));

    /// <inheritdoc />
    public Task UpdateDecisionAsync(
        Guid id,
        ApprovalStatus status,
        Guid decidedBy,
        DateTimeOffset decidedAt,
        string? remark,
        CancellationToken cancellationToken = default)
    {
        Decisions.Add((status, decidedBy, decidedAt, remark));
        if (_approvals.TryGetValue(id, out var approval))
        {
            approval.Status = status;
            approval.DecidedBy = decidedBy;
            approval.DecidedAt = decidedAt;
            approval.DecisionRemark = remark;
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// 审批用例的常用测试替身工厂：让既有四类单据创建用例的构造调用保持一行（042 引入的新依赖）。
/// 站内信写入通道与接收人反查复用 041 的 <see cref="FakeNotificationWriter"/> / <see cref="FakePermissionedUserQuery"/>。
/// </summary>
internal static class ApprovalTestStubs
{
    /// <summary>站内信通知器（默认为不连外部的桩）</summary>
    public static ApprovalNotifier Notifier(
        FakeNotificationWriter? writer = null, FakePermissionedUserQuery? users = null)
        => new(writer ?? new FakeNotificationWriter(), users ?? new FakePermissionedUserQuery(),
            NullLogger<ApprovalNotifier>.Instance);
}
