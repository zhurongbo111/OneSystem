using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 岗位仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// </summary>
public sealed class PositionRepository : IPositionRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化岗位仓储
    /// </summary>
    public PositionRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<PositionListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        PositionStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Positions.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(p => p.Code.ToLower().Contains(lower) || p.Name.ToLower().Contains(lower));
        }

        if (status is not null)
        {
            var value = status.Value;
            query = query.Where(p => p.Status == value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PositionListItem
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Status = p.Status,
                Remark = p.Remark,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public Task<Position?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        // 该查询同时服务于详情读取与编辑 / 启停 / 删除，需跟踪实体以便后续更新，故不使用 AsNoTracking
        => _dbContext.Positions.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var lower = code.Trim().ToLowerInvariant();
        var query = _dbContext.Positions.Where(p => p.Code.ToLower() == lower);
        if (excludeId is not null)
        {
            var exclude = excludeId.Value;
            query = query.Where(p => p.Id != exclude);
        }

        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var lower = name.Trim().ToLowerInvariant();
        var query = _dbContext.Positions.Where(p => p.Name.ToLower() == lower);
        if (excludeId is not null)
        {
            var exclude = excludeId.Value;
            query = query.Where(p => p.Id != exclude);
        }

        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> CountEmployeesAsync(Guid positionId, CancellationToken cancellationToken = default)
        => _dbContext.Employees.CountAsync(e => e.PositionId == positionId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PositionPickItem>> GetPickListAsync(CancellationToken cancellationToken = default)
        => await _dbContext.Positions.AsNoTracking()
            .Where(p => p.Status == PositionStatus.Enabled)
            .OrderBy(p => p.Code)
            .Select(p => new PositionPickItem { Id = p.Id, Code = p.Code, Name = p.Name })
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(Position position, CancellationToken cancellationToken = default)
    {
        _dbContext.Positions.Add(position);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Position position, CancellationToken cancellationToken = default)
    {
        _dbContext.Positions.Update(position);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Position position, CancellationToken cancellationToken = default)
    {
        _dbContext.Positions.Remove(position);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
