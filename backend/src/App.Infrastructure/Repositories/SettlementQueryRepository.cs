using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 结算跨表只读查询仓储的 EF Core 实现（PostgreSQL）：未结单据候选 + 往来对账台账。
/// 只读、不做业务判定；未结候选在内存合并分页（单个往来的未结单据量小，见 design.md §5）。
/// </summary>
public sealed class SettlementQueryRepository : ISettlementQueryRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化结算跨表查询仓储
    /// </summary>
    public SettlementQueryRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<SettlementCandidateItem> Items, int Total)> GetUnsettledAsync(
        Guid partnerId,
        SettlementType type,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var candidates = new List<SettlementCandidateItem>();

        if (type == SettlementType.Receipt)
        {
            // 收款方向：销售出库单（客户欠我们）+ 采购退货单（供应商欠我们）
            candidates.AddRange(await _dbContext.SalesOrders.AsNoTracking()
                .Where(o => o.PartnerId == partnerId && o.Status == OrderStatus.Normal && o.SettledAmount < o.TotalAmount)
                .Select(o => new SettlementCandidateItem
                {
                    OrderType = SettlementOrderType.SalesOutbound,
                    OrderId = o.Id,
                    OrderNo = o.OrderNo,
                    OrderDate = o.OrderDate,
                    TotalAmount = o.TotalAmount,
                    SettledAmount = o.SettledAmount,
                })
                .ToListAsync(cancellationToken));

            candidates.AddRange(await _dbContext.PurchaseReturns.AsNoTracking()
                .Where(r => r.PartnerId == partnerId && r.Status == OrderStatus.Normal && r.SettledAmount < r.TotalAmount)
                .Select(r => new SettlementCandidateItem
                {
                    OrderType = SettlementOrderType.PurchaseReturn,
                    OrderId = r.Id,
                    OrderNo = r.ReturnNo,
                    OrderDate = r.ReturnDate,
                    TotalAmount = r.TotalAmount,
                    SettledAmount = r.SettledAmount,
                })
                .ToListAsync(cancellationToken));
        }
        else
        {
            // 付款方向：采购入库单（我们欠供应商）+ 销售退货单（我们欠客户）
            candidates.AddRange(await _dbContext.PurchaseOrders.AsNoTracking()
                .Where(o => o.PartnerId == partnerId && o.Status == OrderStatus.Normal && o.SettledAmount < o.TotalAmount)
                .Select(o => new SettlementCandidateItem
                {
                    OrderType = SettlementOrderType.PurchaseInbound,
                    OrderId = o.Id,
                    OrderNo = o.OrderNo,
                    OrderDate = o.OrderDate,
                    TotalAmount = o.TotalAmount,
                    SettledAmount = o.SettledAmount,
                })
                .ToListAsync(cancellationToken));

            candidates.AddRange(await _dbContext.SalesReturns.AsNoTracking()
                .Where(r => r.PartnerId == partnerId && r.Status == OrderStatus.Normal && r.SettledAmount < r.TotalAmount)
                .Select(r => new SettlementCandidateItem
                {
                    OrderType = SettlementOrderType.SalesReturn,
                    OrderId = r.Id,
                    OrderNo = r.ReturnNo,
                    OrderDate = r.ReturnDate,
                    TotalAmount = r.TotalAmount,
                    SettledAmount = r.SettledAmount,
                })
                .ToListAsync(cancellationToken));
        }

        var total = candidates.Count;
        var items = candidates
            .OrderByDescending(c => c.OrderDate)
            .ThenByDescending(c => c.OrderNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<ReconciliationItem> Items, int Total)> GetReconciliationAsync(
        string? keyword,
        PartnerType? type,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var partnerQuery = _dbContext.Partners.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            partnerQuery = partnerQuery.Where(p => p.Name.ToLower().Contains(lower)
                || (p.Contact != null && p.Contact.ToLower().Contains(lower)));
        }

        if (type is not null)
        {
            var value = type.Value;
            partnerQuery = partnerQuery.Where(p => p.Type == value);
        }

        var total = await partnerQuery.CountAsync(cancellationToken);
        var partners = await partnerQuery
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (partners.Count == 0)
        {
            return (Array.Empty<ReconciliationItem>(), total);
        }

        var ids = partners.Select(p => p.Id).ToList();

        // 四类单据按往来聚合：总额 + 未结单据数（未作废且未结金额 > 0）
        var salesOrders = await _dbContext.SalesOrders.AsNoTracking()
            .Where(o => ids.Contains(o.PartnerId) && o.Status == OrderStatus.Normal)
            .GroupBy(o => o.PartnerId)
            .Select(g => new
            {
                PartnerId = g.Key,
                Amount = g.Sum(o => o.TotalAmount),
                UnsettledCount = g.Count(o => o.SettledAmount < o.TotalAmount),
            })
            .ToListAsync(cancellationToken);

        var salesReturns = await _dbContext.SalesReturns.AsNoTracking()
            .Where(r => ids.Contains(r.PartnerId) && r.Status == OrderStatus.Normal)
            .GroupBy(r => r.PartnerId)
            .Select(g => new
            {
                PartnerId = g.Key,
                Amount = g.Sum(r => r.TotalAmount),
                UnsettledCount = g.Count(r => r.SettledAmount < r.TotalAmount),
            })
            .ToListAsync(cancellationToken);

        var purchaseOrders = await _dbContext.PurchaseOrders.AsNoTracking()
            .Where(o => ids.Contains(o.PartnerId) && o.Status == OrderStatus.Normal)
            .GroupBy(o => o.PartnerId)
            .Select(g => new
            {
                PartnerId = g.Key,
                Amount = g.Sum(o => o.TotalAmount),
                UnsettledCount = g.Count(o => o.SettledAmount < o.TotalAmount),
            })
            .ToListAsync(cancellationToken);

        var purchaseReturns = await _dbContext.PurchaseReturns.AsNoTracking()
            .Where(r => ids.Contains(r.PartnerId) && r.Status == OrderStatus.Normal)
            .GroupBy(r => r.PartnerId)
            .Select(g => new
            {
                PartnerId = g.Key,
                Amount = g.Sum(r => r.TotalAmount),
                UnsettledCount = g.Count(r => r.SettledAmount < r.TotalAmount),
            })
            .ToListAsync(cancellationToken);

        // 已收 / 已付：收付款单总额（未作废）
        var settlements = await _dbContext.Settlements.AsNoTracking()
            .Where(s => ids.Contains(s.PartnerId) && s.Status == OrderStatus.Normal)
            .GroupBy(s => new { s.PartnerId, s.Type })
            .Select(g => new { g.Key.PartnerId, g.Key.Type, Amount = g.Sum(s => s.TotalAmount) })
            .ToListAsync(cancellationToken);

        var salesOrderMap = salesOrders.ToDictionary(x => x.PartnerId);
        var salesReturnMap = salesReturns.ToDictionary(x => x.PartnerId);
        var purchaseOrderMap = purchaseOrders.ToDictionary(x => x.PartnerId);
        var purchaseReturnMap = purchaseReturns.ToDictionary(x => x.PartnerId);

        var items = partners.Select(p =>
        {
            var salesTotal = salesOrderMap.TryGetValue(p.Id, out var so) ? so.Amount : 0m;
            var salesReturnTotal = salesReturnMap.TryGetValue(p.Id, out var sr) ? sr.Amount : 0m;
            var purchaseTotal = purchaseOrderMap.TryGetValue(p.Id, out var po) ? po.Amount : 0m;
            var purchaseReturnTotal = purchaseReturnMap.TryGetValue(p.Id, out var pr) ? pr.Amount : 0m;

            var received = settlements
                .Where(s => s.PartnerId == p.Id && s.Type == SettlementType.Receipt)
                .Sum(s => s.Amount);
            var paid = settlements
                .Where(s => s.PartnerId == p.Id && s.Type == SettlementType.Payment)
                .Sum(s => s.Amount);

            var unsettledCount =
                (salesOrderMap.TryGetValue(p.Id, out var so2) ? so2.UnsettledCount : 0)
                + (salesReturnMap.TryGetValue(p.Id, out var sr2) ? sr2.UnsettledCount : 0)
                + (purchaseOrderMap.TryGetValue(p.Id, out var po2) ? po2.UnsettledCount : 0)
                + (purchaseReturnMap.TryGetValue(p.Id, out var pr2) ? pr2.UnsettledCount : 0);

            return new ReconciliationItem
            {
                PartnerId = p.Id,
                PartnerName = p.Name,
                PartnerType = p.Type,
                ReceivableAmount = salesTotal - salesReturnTotal - received,
                PayableAmount = purchaseTotal - purchaseReturnTotal - paid,
                UnsettledOrderCount = unsettledCount,
            };
        }).ToList();

        return (items, total);
    }
}
