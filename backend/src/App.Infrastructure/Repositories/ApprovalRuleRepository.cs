using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 审批规则仓储的 EF Core 实现（PostgreSQL，specs/042-erp-approval/design.md §3.2）。
/// 只做数据访问，不做业务判定；审计字段由 Handler 经 ICurrentUser 获取后随方法参数传入。
/// </summary>
public sealed class ApprovalRuleRepository : IApprovalRuleRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化审批规则仓储
    /// </summary>
    public ApprovalRuleRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApprovalRule>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _dbContext.ApprovalRules.AsNoTracking()
            .OrderBy(r => r.OrderType)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<ApprovalRule?> GetAsync(SettlementOrderType orderType, CancellationToken cancellationToken = default)
        => await _dbContext.ApprovalRules.AsNoTracking()
            .FirstOrDefaultAsync(r => r.OrderType == orderType, cancellationToken);

    /// <inheritdoc />
    public async Task UpsertAsync(
        SettlementOrderType orderType,
        decimal thresholdAmount,
        bool enabled,
        Guid? operatorId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.ApprovalRules
            .FirstOrDefaultAsync(r => r.OrderType == orderType, cancellationToken);

        if (existing is null)
        {
            _dbContext.ApprovalRules.Add(new ApprovalRule
            {
                Id = Guid.NewGuid(),
                OrderType = orderType,
                ThresholdAmount = thresholdAmount,
                Enabled = enabled,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
                CreatedBy = operatorId,
                UpdatedBy = operatorId,
            });
        }
        else
        {
            existing.ThresholdAmount = thresholdAmount;
            existing.Enabled = enabled;
            existing.UpdatedAt = utcNow;
            existing.UpdatedBy = operatorId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
