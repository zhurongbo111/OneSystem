using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 月度工资单仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定
/// （044-erp-hcm-payroll/design.md §3.1）。
/// </summary>
public sealed class PayrollRepository : IPayrollRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化工资单仓储
    /// </summary>
    public PayrollRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Payroll> Items, int Total)> GetPagedAsync(
        int? year,
        int? month,
        Guid? employeeId,
        PayrollStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Payrolls.AsNoTracking().AsQueryable();

        if (year is not null)
        {
            var value = year.Value;
            query = query.Where(p => p.Year == value);
        }

        if (month is not null)
        {
            var value = month.Value;
            query = query.Where(p => p.Month == value);
        }

        if (employeeId is not null)
        {
            var value = employeeId.Value;
            query = query.Where(p => p.EmployeeId == value);
        }

        if (status is not null)
        {
            var value = status.Value;
            query = query.Where(p => p.Status == value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Month)
            .ThenByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public Task<Payroll?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        // 该查询服务于编辑 / 发放 / 删除，需跟踪实体以便后续更新，故不使用 AsNoTracking
        => _dbContext.Payrolls.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid employeeId, int year, int month, CancellationToken cancellationToken = default)
        => _dbContext.Payrolls.AsNoTracking()
            .AnyAsync(p => p.EmployeeId == employeeId && p.Year == year && p.Month == month, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Employee>> GetEmployeesForPeriodAsync(
        int year, int month, CancellationToken cancellationToken = default)
    {
        // 期间末 = 该月最后一天；入职晚于期间末的员工不参与本期生成
        var periodEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        return await _dbContext.Employees.AsNoTracking()
            .Where(e => e.Status == EmployeeStatus.Active && e.HireDate <= periodEnd)
            .OrderBy(e => e.EmployeeNo)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(Payroll payroll, CancellationToken cancellationToken = default)
    {
        _dbContext.Payrolls.Add(payroll);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddRangeAsync(IReadOnlyList<Payroll> payrolls, CancellationToken cancellationToken = default)
    {
        if (payrolls.Count == 0)
        {
            return;
        }

        _dbContext.Payrolls.AddRange(payrolls);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Payroll payroll, CancellationToken cancellationToken = default)
    {
        _dbContext.Payrolls.Update(payroll);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Payroll payroll, CancellationToken cancellationToken = default)
    {
        _dbContext.Payrolls.Remove(payroll);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
