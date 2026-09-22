using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 发票仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// 审计字段统一由 Handler 经 ICurrentUser 获取后随方法参数 / 实体传入，仓储不感知当前用户。
/// </summary>
public sealed class InvoiceRepository : IInvoiceRepository
{
    /// <summary>PostgreSQL unique_violation 的 SQLSTATE</summary>
    private const string UniqueViolationSqlState = "23505";

    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化发票仓储
    /// </summary>
    public InvoiceRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<InvoiceListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        InvoiceType? type,
        Guid? partnerId,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Invoices.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            // 关键词命中发票号 / 往来名称，或任一关联明细的单据号（EXISTS 子查询，见 design.md §3.1）
            query = query.Where(v => v.InvoiceNo.ToLower().Contains(lower)
                || v.PartnerName.ToLower().Contains(lower)
                || _dbContext.InvoiceItems.Any(i => i.InvoiceId == v.Id && i.OrderNo.ToLower().Contains(lower)));
        }

        if (type is not null)
        {
            var value = type.Value;
            query = query.Where(v => v.Type == value);
        }

        if (partnerId is not null)
        {
            var value = partnerId.Value;
            query = query.Where(v => v.PartnerId == value);
        }

        // 日期范围对 InvoiceDate 闭区间比较（timestamptz 语义，不做时区归一化）
        if (start is not null)
        {
            var s = start.Value;
            query = query.Where(v => v.InvoiceDate >= s);
        }

        if (end is not null)
        {
            var e = end.Value;
            query = query.Where(v => v.InvoiceDate <= e);
        }

        var total = await query.CountAsync(cancellationToken);
        var invoices = await query
            .OrderByDescending(v => v.CreatedAt)
            .ThenBy(v => v.InvoiceNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // 本页关联明细一次批量取回（避免逐单 N+1），拼出列表的「关联单据」摘要
        var items = await GetItemsByInvoiceIdsAsync(invoices.Select(v => v.Id).ToList(), cancellationToken);
        var orderNosByInvoice = items
            .GroupBy(i => i.InvoiceId)
            .ToDictionary(g => g.Key, g => string.Join("、", g.Select(i => i.OrderNo)));

        var listItems = invoices.Select(v => new InvoiceListItem
        {
            Id = v.Id,
            InvoiceNo = v.InvoiceNo,
            Type = v.Type,
            PartnerName = v.PartnerName,
            InvoiceDate = v.InvoiceDate,
            AmountExcludingTax = v.AmountExcludingTax,
            TaxRate = v.TaxRate,
            TaxAmount = v.TaxAmount,
            TotalAmount = v.TotalAmount,
            Status = v.Status,
            CreatedAt = v.CreatedAt,
            OrderNoSummary = orderNosByInvoice.TryGetValue(v.Id, out var summary) ? summary : string.Empty,
        }).ToList();

        return (listItems, total);
    }

    /// <inheritdoc />
    public async Task<(Invoice? Invoice, IReadOnlyList<InvoiceItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        if (invoice is null)
        {
            return (null, Array.Empty<InvoiceItem>());
        }

        // 关联明细按插入顺序（明细 Id 为顺序 Guid，与 AddRange 顺序一致）
        var items = await _dbContext.InvoiceItems.AsNoTracking()
            .Where(i => i.InvoiceId == id)
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);

        return (invoice, items);
    }

    /// <inheritdoc />
    public Task<bool> ExistsByInvoiceNoAsync(string invoiceNo, CancellationToken cancellationToken = default)
    {
        // 发票号唯一按「忽略大小写」判定（与 012 商品编码同口径；唯一索引为并发兜底）
        var lower = invoiceNo.Trim().ToLowerInvariant();
        return _dbContext.Invoices.AnyAsync(v => v.InvoiceNo.ToLower() == lower, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(Invoice invoice, IReadOnlyList<InvoiceItem> items, CancellationToken cancellationToken = default)
    {
        _dbContext.Invoices.Add(invoice);
        _dbContext.InvoiceItems.AddRange(items);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState })
        {
            // 发票号唯一约束冲突（并发窗口）：直接映射为业务码 40132，无需重试（号码由用户录入，不自动生成）
            throw new BusinessException(ErrorCode.InvoiceNoExists, "发票号已存在");
        }
    }

    /// <inheritdoc />
    public async Task UpdateStatusAsync(Guid id, OrderStatus status, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (invoice is null)
        {
            return;
        }

        invoice.Status = status;
        invoice.UpdatedAt = DateTimeOffset.UtcNow;
        invoice.UpdatedBy = operatorId;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InvoiceItem>> GetItemsByInvoiceIdsAsync(IReadOnlyCollection<Guid> invoiceIds, CancellationToken cancellationToken = default)
    {
        if (invoiceIds.Count == 0)
        {
            return Array.Empty<InvoiceItem>();
        }

        // 导出 / 列表聚合共用：一次查询避免逐单 N+1；按明细 Id 升序即插入顺序
        return await _dbContext.InvoiceItems.AsNoTracking()
            .Where(i => invoiceIds.Contains(i.InvoiceId))
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);
    }
}