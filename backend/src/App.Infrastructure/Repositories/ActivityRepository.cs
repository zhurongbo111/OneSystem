using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 跟进活动仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// 活动是纯追加表：本仓储只提供查询与新增；记录人显示名按 id 批量取回（避免逐行联查）。
/// </summary>
public sealed class ActivityRepository : IActivityRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化跟进活动仓储
    /// </summary>
    public ActivityRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActivityItem>> GetByBizAsync(
        ActivityBizType bizType,
        Guid bizId,
        CancellationToken cancellationToken = default)
    {
        // 跟进时间倒序（最近的在最前）；同一时间按顺序 Guid 倒序，与记录顺序一致
        var activities = await _dbContext.Activities.AsNoTracking()
            .Where(a => a.BizType == bizType && a.BizId == bizId)
            .OrderByDescending(a => a.ActivityTime)
            .ThenByDescending(a => a.Id)
            .ToListAsync(cancellationToken);

        // 记录人显示名：本页活动一次批量取回（避免逐行往返）
        var userIds = activities
            .Where(a => a.CreatedBy is not null)
            .Select(a => a.CreatedBy!.Value)
            .Distinct()
            .ToList();

        Dictionary<Guid, string> recorderNames = userIds.Count == 0
            ? []
            : await _dbContext.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        return activities
            .Select(a => new ActivityItem
            {
                Activity = a,
                RecorderName = a.CreatedBy is not null && recorderNames.TryGetValue(a.CreatedBy.Value, out var name) ? name : null,
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task AddAsync(Activity activity, CancellationToken cancellationToken = default)
    {
        _dbContext.Activities.Add(activity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
