using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 部门仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// 树形由 <c>ParentId</c> 自引用表达，本实现一次取全量后组装为树（单组织量级）
/// （specs/030-erp-org-employee/design.md §5）。
/// </summary>
public sealed class DepartmentRepository : IDepartmentRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化部门仓储
    /// </summary>
    public DepartmentRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DepartmentTreeNode>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        // 一次取全量（含停用部门：树需完整展示，停用仅影响可选性），同级按排序升序
        var departments = await _dbContext.Departments.AsNoTracking()
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Code)
            .ToListAsync(cancellationToken);

        var employeeCounts = await GetEmployeeCountsAsync(cancellationToken);
        var childrenByParent = departments.ToLookup(d => d.ParentId);

        DepartmentTreeNode Build(Department department)
            => new()
            {
                Id = department.Id,
                Code = department.Code,
                Name = department.Name,
                ParentId = department.ParentId,
                SortOrder = department.SortOrder,
                Status = department.Status,
                Remark = department.Remark,
                EmployeeCount = employeeCounts.TryGetValue(department.Id, out var count) ? count : 0,
                Children = childrenByParent[department.Id].Select(Build).ToList(),
            };

        return childrenByParent[null].Select(Build).ToList();
    }

    /// <inheritdoc />
    public Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        // 该查询同时服务于详情读取与编辑 / 启停 / 删除，需跟踪实体以便后续更新，故不使用 AsNoTracking
        => _dbContext.Departments.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var lower = code.Trim().ToLowerInvariant();
        var query = _dbContext.Departments.Where(d => d.Code.ToLower() == lower);
        if (excludeId is not null)
        {
            var exclude = excludeId.Value;
            query = query.Where(d => d.Id != exclude);
        }

        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> ExistsByNameAsync(string name, Guid? parentId, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var lower = name.Trim().ToLowerInvariant();
        var query = _dbContext.Departments.Where(d => d.ParentId == parentId && d.Name.ToLower() == lower);
        if (excludeId is not null)
        {
            var exclude = excludeId.Value;
            query = query.Where(d => d.Id != exclude);
        }

        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Departments.AnyAsync(d => d.ParentId == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, int>> GetEmployeeCountsAsync(CancellationToken cancellationToken = default)
    {
        // 只统计在职员工（离职员工不占用部门，树节点「在职人数」口径）
        var rows = await _dbContext.Employees.AsNoTracking()
            .Where(e => e.DepartmentId != null && e.Status == EmployeeStatus.Active)
            .GroupBy(e => e.DepartmentId!.Value)
            .Select(group => new { DepartmentId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.DepartmentId, row => row.Count);
    }

    /// <inheritdoc />
    public Task<int> CountEmployeesAsync(Guid departmentId, CancellationToken cancellationToken = default)
        // 含离职员工：员工表对部门建的是 Restrict 外键，残留引用同样会阻止删除
        => _dbContext.Employees.CountAsync(e => e.DepartmentId == departmentId, cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(Department department, CancellationToken cancellationToken = default)
    {
        _dbContext.Departments.Add(department);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Department department, CancellationToken cancellationToken = default)
    {
        _dbContext.Departments.Update(department);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Department department, CancellationToken cancellationToken = default)
    {
        _dbContext.Departments.Remove(department);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
