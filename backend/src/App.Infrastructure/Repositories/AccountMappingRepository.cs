using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 科目映射仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定
/// （specs/033-erp-general-ledger/design.md §3.1）。
/// </summary>
public sealed class AccountMappingRepository : IAccountMappingRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化科目映射仓储
    /// </summary>
    public AccountMappingRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountMapping>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _dbContext.AccountMappings.AsNoTracking()
            .OrderBy(m => m.Key)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<AccountMapping> UpsertAsync(string key, Guid accountId, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        var mapping = await _dbContext.AccountMappings.FirstOrDefaultAsync(m => m.Key == key, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (mapping is null)
        {
            mapping = new AccountMapping
            {
                Id = Guid.NewGuid(),
                Key = key,
                AccountId = accountId,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = operatorId,
                UpdatedBy = operatorId,
            };
            _dbContext.AccountMappings.Add(mapping);
        }
        else
        {
            mapping.AccountId = accountId;
            mapping.UpdatedAt = now;
            mapping.UpdatedBy = operatorId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return mapping;
    }
}
