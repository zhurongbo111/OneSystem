using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 员工仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// 列表 / 详情的部门名、岗位名、关联账号显示名以子查询联查带出，避免逐行 N+1
/// （specs/030-erp-org-employee/design.md §3.1）。
/// </summary>
public sealed class EmployeeRepository : IEmployeeRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化员工仓储
    /// </summary>
    public EmployeeRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<EmployeeListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? departmentId,
        Guid? positionId,
        EmployeeStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyFilters(keyword, departmentId, positionId, status);

        var total = await query.CountAsync(cancellationToken);
        var items = await ProjectListItems(query)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmployeeListItem>> GetAllForExportAsync(
        string? keyword,
        Guid? departmentId,
        Guid? positionId,
        EmployeeStatus? status,
        int limit,
        CancellationToken cancellationToken = default)
        => await ProjectListItems(ApplyFilters(keyword, departmentId, positionId, status))
            .Take(limit)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        // 该查询服务于编辑 / 启停，需跟踪实体以便后续更新，故不使用 AsNoTracking
        => _dbContext.Employees.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<EmployeeDetail?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Employees.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new EmployeeDetail
            {
                Id = e.Id,
                EmployeeNo = e.EmployeeNo,
                Name = e.Name,
                Gender = e.Gender,
                Phone = e.Phone,
                Email = e.Email,
                DepartmentId = e.DepartmentId,
                DepartmentName = _dbContext.Departments.Where(d => d.Id == e.DepartmentId).Select(d => d.Name).FirstOrDefault(),
                PositionId = e.PositionId,
                PositionName = _dbContext.Positions.Where(p => p.Id == e.PositionId).Select(p => p.Name).FirstOrDefault(),
                HireDate = e.HireDate,
                ResignDate = e.ResignDate,
                Status = e.Status,
                UserId = e.UserId,
                UserDisplayName = _dbContext.Users.Where(u => u.Id == e.UserId).Select(u => u.DisplayName).FirstOrDefault(),
                Remark = e.Remark,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt,
            })
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExistsByNoAsync(string employeeNo, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var lower = employeeNo.Trim().ToLowerInvariant();
        var query = _dbContext.Employees.Where(e => e.EmployeeNo.ToLower() == lower);
        if (excludeId is not null)
        {
            var exclude = excludeId.Value;
            query = query.Where(e => e.Id != exclude);
        }

        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> ExistsByPhoneAsync(string phone, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var value = phone.Trim();
        var query = _dbContext.Employees.Where(e => e.Phone != null && e.Phone == value);
        if (excludeId is not null)
        {
            var exclude = excludeId.Value;
            query = query.Where(e => e.Id != exclude);
        }

        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var lower = email.Trim().ToLowerInvariant();
        var query = _dbContext.Employees.Where(e => e.Email != null && e.Email.ToLower() == lower);
        if (excludeId is not null)
        {
            var exclude = excludeId.Value;
            query = query.Where(e => e.Id != exclude);
        }

        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> ExistsByUserIdAsync(Guid userId, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Employees.Where(e => e.UserId == userId);
        if (excludeId is not null)
        {
            var exclude = excludeId.Value;
            query = query.Where(e => e.Id != exclude);
        }

        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmployeePickUserItem>> GetAvailableUsersAsync(Guid? employeeId, CancellationToken cancellationToken = default)
    {
        var boundUserIds = await _dbContext.Employees.AsNoTracking()
            .Where(e => e.UserId != null)
            .Select(e => e.UserId!.Value)
            .ToListAsync(cancellationToken);

        // 编辑场景额外放行「当前员工已绑定的账号」（否则当前绑定值不在候选里，会被误清空）
        Guid? currentBoundUserId = null;
        if (employeeId is not null)
        {
            var currentEmployeeId = employeeId.Value;
            currentBoundUserId = await _dbContext.Employees.AsNoTracking()
                .Where(e => e.Id == currentEmployeeId)
                .Select(e => e.UserId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await _dbContext.Users.AsNoTracking()
            .Where(u => (u.Status == UserStatus.Enabled && !boundUserIds.Contains(u.Id))
                || (currentBoundUserId != null && u.Id == currentBoundUserId.Value))
            .OrderBy(u => u.Username)
            .Select(u => new EmployeePickUserItem { Id = u.Id, Username = u.Username, DisplayName = u.DisplayName })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        _dbContext.Employees.Add(employee);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        _dbContext.Employees.Update(employee);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>列表 / 导出共用筛选（口径单处维护）</summary>
    private IQueryable<Employee> ApplyFilters(string? keyword, Guid? departmentId, Guid? positionId, EmployeeStatus? status)
    {
        var query = _dbContext.Employees.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(e => e.EmployeeNo.ToLower().Contains(lower) || e.Name.ToLower().Contains(lower));
        }

        if (departmentId is not null)
        {
            var value = departmentId.Value;
            query = query.Where(e => e.DepartmentId == value);
        }

        if (positionId is not null)
        {
            var value = positionId.Value;
            query = query.Where(e => e.PositionId == value);
        }

        if (status is not null)
        {
            var value = status.Value;
            query = query.Where(e => e.Status == value);
        }

        return query.OrderByDescending(e => e.CreatedAt).ThenBy(e => e.EmployeeNo);
    }

    /// <summary>列表项投影（联查部门 / 岗位 / 账号名称）</summary>
    private IQueryable<EmployeeListItem> ProjectListItems(IQueryable<Employee> query)
        => query.Select(e => new EmployeeListItem
        {
            Id = e.Id,
            EmployeeNo = e.EmployeeNo,
            Name = e.Name,
            Gender = e.Gender,
            Phone = e.Phone,
            DepartmentId = e.DepartmentId,
            DepartmentName = _dbContext.Departments.Where(d => d.Id == e.DepartmentId).Select(d => d.Name).FirstOrDefault(),
            PositionId = e.PositionId,
            PositionName = _dbContext.Positions.Where(p => p.Id == e.PositionId).Select(p => p.Name).FirstOrDefault(),
            HireDate = e.HireDate,
            ResignDate = e.ResignDate,
            Status = e.Status,
            UserId = e.UserId,
            UserDisplayName = _dbContext.Users.Where(u => u.Id == e.UserId).Select(u => u.DisplayName).FirstOrDefault(),
            CreatedAt = e.CreatedAt,
            CreatedBy = e.CreatedBy,
        });
}
