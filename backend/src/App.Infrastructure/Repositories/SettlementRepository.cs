using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 收付款单仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// 审计字段统一由 Handler 经 ICurrentUser 获取后随方法参数 / 实体传入，仓储不感知当前用户。
/// </summary>
public sealed class SettlementRepository : ISettlementRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化收付款单仓储
    /// </summary>
    public SettlementRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<SettlementListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        SettlementType? type,
        Guid? partnerId,
        SettlementMethod? method,
        DateTimeOffset? start,
        DateTimeOffset? end,
        SettlementOrderType? orderType,
        Guid? orderId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Settlements.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(s => s.SettlementNo.ToLower().Contains(lower) || s.PartnerName.ToLower().Contains(lower));
        }

        if (type is not null)
        {
            var value = type.Value;
            query = query.Where(s => s.Type == value);
        }

        if (partnerId is not null)
        {
            var value = partnerId.Value;
            query = query.Where(s => s.PartnerId == value);
        }

        if (method is not null)
        {
            var value = method.Value;
            query = query.Where(s => s.Method == value);
        }

        // 日期范围对 SettlementDate 闭区间比较（timestamptz 语义，不做时区归一化）
        if (start is not null)
        {
            var s = start.Value;
            query = query.Where(x => x.SettlementDate >= s);
        }

        if (end is not null)
        {
            var e = end.Value;
            query = query.Where(x => x.SettlementDate <= e);
        }

        // 按被核销单据反查（单据详情「收付款明细」）：仅保留命中了该单据核销明细的收付款单；
        // orderType 与 orderId 由 Validator 保证成对传入（SettlementItems.OrderId 有索引）
        if (orderId is not null && orderType is not null)
        {
            var oid = orderId.Value;
            var otype = orderType.Value;
            query = query.Where(s => _dbContext.SettlementItems.Any(i =>
                i.SettlementId == s.Id && i.OrderId == oid && i.OrderType == otype));
        }

        var total = await query.CountAsync(cancellationToken);

        // 资金账户名称（034-erp-cash）：左连接 BankAccounts 带出，未关联账户时为 null
        var joined = from s in query
                     join b in _dbContext.BankAccounts.AsNoTracking() on s.BankAccountId equals b.Id into accounts
                     from b in accounts.DefaultIfEmpty()
                     select new { Settlement = s, BankAccountName = (string?)b!.Name };

        var items = await joined
            .OrderByDescending(x => x.Settlement.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SettlementListItem
            {
                Id = x.Settlement.Id,
                SettlementNo = x.Settlement.SettlementNo,
                Type = x.Settlement.Type,
                PartnerId = x.Settlement.PartnerId,
                PartnerName = x.Settlement.PartnerName,
                SettlementDate = x.Settlement.SettlementDate,
                TotalAmount = x.Settlement.TotalAmount,
                Method = x.Settlement.Method,
                BankAccountId = x.Settlement.BankAccountId,
                BankAccountName = x.BankAccountName,
                Status = x.Settlement.Status,
                CreatedBy = x.Settlement.CreatedBy,
                CreatedAt = x.Settlement.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<(SettlementDetail? Settlement, IReadOnlyList<SettlementItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // 资金账户名称（034-erp-cash）：左连接带出，未关联账户时为 null
        var settlement = await (from s in _dbContext.Settlements.AsNoTracking().Where(s => s.Id == id)
                                join b in _dbContext.BankAccounts.AsNoTracking() on s.BankAccountId equals b.Id into accounts
                                from b in accounts.DefaultIfEmpty()
                                select new SettlementDetail
                                {
                                    Id = s.Id,
                                    SettlementNo = s.SettlementNo,
                                    Type = s.Type,
                                    PartnerId = s.PartnerId,
                                    PartnerName = s.PartnerName,
                                    SettlementDate = s.SettlementDate,
                                    TotalAmount = s.TotalAmount,
                                    Method = s.Method,
                                    BankAccountId = s.BankAccountId,
                                    BankAccountName = (string?)b!.Name,
                                    Status = s.Status,
                                    Remark = s.Remark,
                                    CreatedBy = s.CreatedBy,
                                    CreatedAt = s.CreatedAt,
                                })
            .FirstOrDefaultAsync(cancellationToken);

        if (settlement is null)
        {
            return (null, Array.Empty<SettlementItem>());
        }

        // 核销明细按插入顺序（明细 Id 为顺序 Guid，与 AddRange 顺序一致）
        var items = await _dbContext.SettlementItems.AsNoTracking()
            .Where(i => i.SettlementId == id)
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);

        return (settlement, items);
    }

    /// <inheritdoc />
    public async Task AddAsync(Settlement settlement, IReadOnlyList<SettlementItem> items, CancellationToken cancellationToken = default)
    {
        _dbContext.Settlements.Add(settlement);
        _dbContext.SettlementItems.AddRange(items);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // 单号唯一约束冲突（PostgreSQL SQLSTATE 23505 = unique_violation）→ 包装为技术异常，由 Handler 重新生成单号重试
            throw new OrderNoConflictException(ex);
        }
    }

    /// <inheritdoc />
    public Task UpdateStatusAsync(Guid id, OrderStatus status, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return _dbContext.Settlements
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, status)
                .SetProperty(x => x.UpdatedAt, now)
                .SetProperty(x => x.UpdatedBy, operatorId),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> GenerateSettlementNoAsync(SettlementType type, DateTimeOffset settlementDate, CancellationToken cancellationToken = default)
    {
        // 前缀按类型区分（收款 RC / 付款 PY，见 specs/ROADMAP.md §6.7）；
        // 序号 = 当天同前缀已有单号数 + 1；唯一索引兜底并发冲突（Handler 重试）
        var prefix = type == SettlementType.Receipt ? "RC" : "PY";
        var dateSegment = settlementDate.UtcDateTime.ToString("yyyyMMdd");
        var pattern = $"{prefix}{dateSegment}";
        var count = await _dbContext.Settlements.CountAsync(s => s.SettlementNo.StartsWith(pattern), cancellationToken);
        return $"{pattern}{(count + 1):D4}";
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SettlementItem>> GetItemsBySettlementIdsAsync(IReadOnlyCollection<Guid> settlementIds, CancellationToken cancellationToken = default)
    {
        if (settlementIds.Count == 0)
        {
            return Array.Empty<SettlementItem>();
        }

        // 导出用批量取核销明细（一次查询避免逐单 N+1）；按明细 Id 升序即插入顺序
        return await _dbContext.SettlementItems.AsNoTracking()
            .Where(i => settlementIds.Contains(i.SettlementId))
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);
    }
}
