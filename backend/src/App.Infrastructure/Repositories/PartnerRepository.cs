using App.Core.Abstractions;
using App.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 往来单位仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// </summary>
public sealed class PartnerRepository : IPartnerRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化往来单位仓储
    /// </summary>
    public PartnerRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<Partner?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        // 该查询同时服务于详情读取与编辑 / 启停，需跟踪实体以便后续更新，故不使用 AsNoTracking
        => _dbContext.Partners.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var lower = name.Trim().ToLowerInvariant();
        var query = _dbContext.Partners.Where(p => p.Name.ToLower() == lower);
        if (excludeId is not null)
        {
            var exclude = excludeId.Value;
            query = query.Where(p => p.Id != exclude);
        }

        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Partner> Items, int Total)> GetPagedAsync(
        string? keyword,
        PartnerType? type,
        PartnerStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Partners.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(p => p.Name.ToLower().Contains(lower) || (p.Contact != null && p.Contact.ToLower().Contains(lower)));
        }

        if (type is not null)
        {
            var value = type.Value;
            query = query.Where(p => p.Type == value);
        }

        if (status is not null)
        {
            var value = status.Value;
            query = query.Where(p => p.Status == value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public async Task AddAsync(Partner partner, CancellationToken cancellationToken = default)
    {
        _dbContext.Partners.Add(partner);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Partner partner, CancellationToken cancellationToken = default)
    {
        _dbContext.Partners.Update(partner);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
