using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 审批记录仓储的 EF Core 实现（PostgreSQL，specs/042-erp-approval/design.md §3.2）。
/// 只做数据访问，不做业务判定；记录只新增与流转状态，不删除。
/// </summary>
public sealed class ApprovalRepository : IApprovalRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化审批记录仓储
    /// </summary>
    public ApprovalRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task AddAsync(Approval approval, CancellationToken cancellationToken = default)
    {
        _dbContext.Approvals.Add(approval);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Approval> Items, int Total)> GetPagedAsync(
        ApprovalStatus? status,
        SettlementOrderType? orderType,
        Guid? submittedBy,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Approvals.AsNoTracking().AsQueryable();

        if (status is not null)
        {
            var value = status.Value;
            query = query.Where(a => a.Status == value);
        }

        if (orderType is not null)
        {
            var value = orderType.Value;
            query = query.Where(a => a.OrderType == value);
        }

        if (submittedBy is not null)
        {
            var value = submittedBy.Value;
            query = query.Where(a => a.SubmittedBy == value);
        }

        // 提交时间闭区间（timestamptz 语义，不做时区归一化）
        if (start is not null)
        {
            var s = start.Value;
            query = query.Where(a => a.SubmittedAt >= s);
        }

        if (end is not null)
        {
            var e = end.Value;
            query = query.Where(a => a.SubmittedAt <= e);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.SubmittedAt)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<Approval?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _dbContext.Approvals.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<Approval?> GetByOrderAsync(SettlementOrderType orderType, Guid orderId, CancellationToken cancellationToken = default)
        => await _dbContext.Approvals.AsNoTracking()
            .FirstOrDefaultAsync(a => a.OrderType == orderType && a.OrderId == orderId, cancellationToken);

    /// <inheritdoc />
    public Task UpdateDecisionAsync(
        Guid id,
        ApprovalStatus status,
        Guid decidedBy,
        DateTimeOffset decidedAt,
        string? remark,
        CancellationToken cancellationToken = default)
        => _dbContext.Approvals
            .Where(a => a.Id == id)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(a => a.Status, status)
                    .SetProperty(a => a.DecidedBy, decidedBy)
                    .SetProperty(a => a.DecidedAt, decidedAt)
                    .SetProperty(a => a.DecisionRemark, remark),
                cancellationToken);
}
