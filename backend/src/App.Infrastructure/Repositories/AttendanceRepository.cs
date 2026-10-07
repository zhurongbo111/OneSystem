using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 考勤登记仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定
/// （044-erp-hcm-payroll/design.md §3.1）。
/// </summary>
public sealed class AttendanceRepository : IAttendanceRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化考勤仓储
    /// </summary>
    public AttendanceRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Attendance> Items, int Total)> GetPagedAsync(
        Guid? employeeId,
        AttendanceType? type,
        DateOnly? startDate,
        DateOnly? endDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Attendances.AsNoTracking().AsQueryable();

        if (employeeId is not null)
        {
            var value = employeeId.Value;
            query = query.Where(a => a.EmployeeId == value);
        }

        if (type is not null)
        {
            var value = type.Value;
            query = query.Where(a => a.Type == value);
        }

        // 日期范围按区间重叠筛选：记录与 [startDate, endDate] 有交集即命中
        if (startDate is not null)
        {
            var value = startDate.Value;
            query = query.Where(a => a.EndDate >= value);
        }

        if (endDate is not null)
        {
            var value = endDate.Value;
            query = query.Where(a => a.StartDate <= value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.StartDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public Task<Attendance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        // 该查询服务于编辑 / 删除，需跟踪实体以便后续更新，故不使用 AsNoTracking
        => _dbContext.Attendances.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<Attendance?> FindOverlapAsync(
        Guid employeeId,
        AttendanceType type,
        DateOnly startDate,
        DateOnly endDate,
        Guid? excludeId,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Attendances.AsNoTracking()
            .Where(a => a.EmployeeId == employeeId
                && a.Type == type
                && a.StartDate <= endDate
                && a.EndDate >= startDate);

        if (excludeId is not null)
        {
            var exclude = excludeId.Value;
            query = query.Where(a => a.Id != exclude);
        }

        return query.OrderBy(a => a.StartDate).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(Attendance attendance, CancellationToken cancellationToken = default)
    {
        _dbContext.Attendances.Add(attendance);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Attendance attendance, CancellationToken cancellationToken = default)
    {
        _dbContext.Attendances.Update(attendance);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Attendance attendance, CancellationToken cancellationToken = default)
    {
        _dbContext.Attendances.Remove(attendance);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
