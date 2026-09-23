using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 仓库仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// </summary>
public sealed class WarehouseRepository : IWarehouseRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化仓库仓储
    /// </summary>
    public WarehouseRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<Warehouse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        // 该查询同时服务于详情读取与编辑 / 启停 / 设为默认，需跟踪实体以便后续更新，故不使用 AsNoTracking
        => _dbContext.Warehouses.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var lower = code.Trim().ToLowerInvariant();
        var query = _dbContext.Warehouses.Where(w => w.Code.ToLower() == lower);
        if (excludeId is not null)
        {
            var exclude = excludeId.Value;
            query = query.Where(w => w.Id != exclude);
        }

        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var lower = name.Trim().ToLowerInvariant();
        var query = _dbContext.Warehouses.Where(w => w.Name.ToLower() == lower);
        if (excludeId is not null)
        {
            var exclude = excludeId.Value;
            query = query.Where(w => w.Id != exclude);
        }

        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Warehouse> Items, int Total)> GetPagedAsync(
        string? keyword,
        PartnerStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Warehouses.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(w => w.Code.ToLower().Contains(lower) || w.Name.ToLower().Contains(lower));
        }

        if (status is not null)
        {
            var value = status.Value;
            query = query.Where(w => w.Status == value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            // 默认仓置顶，其余按编码升序（保证「默认仓在前」与分页稳定）
            .OrderByDescending(w => w.IsDefault)
            .ThenBy(w => w.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Warehouse>> GetEnabledAsync(CancellationToken cancellationToken = default)
        => await _dbContext.Warehouses
            .AsNoTracking()
            .Where(w => w.Status == PartnerStatus.Enabled)
            .OrderBy(w => w.Code)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<Warehouse?> GetDefaultAsync(CancellationToken cancellationToken = default)
        => _dbContext.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.IsDefault, cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken = default)
    {
        _dbContext.Warehouses.Add(warehouse);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Warehouse warehouse, CancellationToken cancellationToken = default)
    {
        _dbContext.Warehouses.Update(warehouse);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task ClearDefaultAsync(Guid? exceptId, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Warehouses.Where(w => w.IsDefault);
        if (exceptId is not null)
        {
            var except = exceptId.Value;
            query = query.Where(w => w.Id != except);
        }

        return query.ExecuteUpdateAsync(s => s.SetProperty(w => w.IsDefault, false), cancellationToken);
    }
}
