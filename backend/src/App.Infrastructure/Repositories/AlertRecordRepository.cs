using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 告警去重台账仓储的 EF Core 实现（PostgreSQL，041-erp-stock-alert）：只增不改；
/// 「同日一次」由唯一索引 <c>(AlertType, ResourceKey, AlertDate)</c> 兜底。
/// </summary>
public sealed class AlertRecordRepository : IAlertRecordRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化告警去重台账仓储
    /// </summary>
    public AlertRecordRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(
        AlertType alertType,
        string resourceKey,
        DateOnly alertDate,
        CancellationToken cancellationToken = default)
        => _dbContext.AlertRecords
            .AsNoTracking()
            .AnyAsync(a => a.AlertType == alertType && a.ResourceKey == resourceKey && a.AlertDate == alertDate, cancellationToken);

    /// <inheritdoc />
    public async Task AddRangeAsync(IReadOnlyList<AlertRecord> records, CancellationToken cancellationToken = default)
    {
        if (records.Count == 0)
        {
            return;
        }

        _dbContext.AlertRecords.AddRange(records);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
