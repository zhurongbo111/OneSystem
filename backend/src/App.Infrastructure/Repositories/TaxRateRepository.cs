using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 税率仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定
/// （specs/031-erp-finance-master/design.md §3.1）。
/// </summary>
public sealed class TaxRateRepository : ITaxRateRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化税率仓储
    /// </summary>
    public TaxRateRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<TaxRateListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        TaxRateStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.TaxRates.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(t => t.Code.ToLower().Contains(lower) || t.Name.ToLower().Contains(lower));
        }

        if (status is not null)
        {
            var value = status.Value;
            query = query.Where(t => t.Status == value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .ThenBy(t => t.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TaxRateListItem
            {
                Id = t.Id,
                Code = t.Code,
                Name = t.Name,
                Rate = t.Rate,
                Status = t.Status,
                Remark = t.Remark,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public Task<TaxRate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        // 该查询同时服务于详情读取与编辑 / 启停 / 删除，需跟踪实体以便后续更新，故不使用 AsNoTracking
        => _dbContext.TaxRates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var lower = code.Trim().ToLowerInvariant();
        var query = _dbContext.TaxRates.Where(t => t.Code.ToLower() == lower);
        if (excludeId is not null)
        {
            var exclude = excludeId.Value;
            query = query.Where(t => t.Id != exclude);
        }

        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var lower = name.Trim().ToLowerInvariant();
        var query = _dbContext.TaxRates.Where(t => t.Name.ToLower() == lower);
        if (excludeId is not null)
        {
            var exclude = excludeId.Value;
            query = query.Where(t => t.Id != exclude);
        }

        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(TaxRate taxRate, CancellationToken cancellationToken = default)
    {
        _dbContext.TaxRates.Add(taxRate);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(TaxRate taxRate, CancellationToken cancellationToken = default)
    {
        _dbContext.TaxRates.Update(taxRate);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(TaxRate taxRate, CancellationToken cancellationToken = default)
    {
        _dbContext.TaxRates.Remove(taxRate);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}