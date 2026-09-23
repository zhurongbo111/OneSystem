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
            candidates.AddRange(await _dbContext.SalesShipments.AsNoTracking()
                .Where(o => o.PartnerId == partnerId && o.Status == OrderStatus.Normal && o.SettledAmount < o.TotalAmount)
                .Select(o => new SettlementCandidateItem
                {
                    OrderType = SettlementOrderType.SalesOutbound,
                    OrderId = o.Id,
                    OrderNo = o.ShipmentNo,
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
            candidates.AddRange(await _dbContext.PurchaseReceipts.AsNoTracking()
                .Where(o => o.PartnerId == partnerId && o.Status == OrderStatus.Normal && o.SettledAmount < o.TotalAmount)
                .Select(o => new SettlementCandidateItem
                {
                    OrderType = SettlementOrderType.PurchaseInbound,
                    OrderId = o.Id,
                    OrderNo = o.ReceiptNo,
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
    public async Task<decimal> GetReceivableAmountAsync(Guid partnerId, CancellationToken cancellationToken = default)
    {
        // 应收口径与 GetReconciliationAsync 完全一致（specs/023-erp-settlement/design.md §0）：
        // 销售出库单未结 + 采购退货单未结，仅计未作废单据
        var salesShipmentUnsettled = await _dbContext.SalesShipments.AsNoTracking()
            .Where(o => o.PartnerId == partnerId && o.Status == OrderStatus.Normal)
            .SumAsync(o => o.TotalAmount - o.SettledAmount, cancellationToken);

        var purchaseReturnUnsettled = await _dbContext.PurchaseReturns.AsNoTracking()
            .Where(r => r.PartnerId == partnerId && r.Status == OrderStatus.Normal)
            .SumAsync(r => r.TotalAmount - r.SettledAmount, cancellationToken);

        return salesShipmentUnsettled + purchaseReturnUnsettled;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<ReconciliationItem> Items, int Total)> GetReconciliationAsync(
        string? keyword,
        PartnerType? type,
        bool overdueOnly,
        DateOnly today,
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

        // 逾期依赖派生值（到期日 / 逾期天数），「仅看逾期」无法下推到 SQL：
        // 故先取全部匹配的往来单位（仅投影派生所需字段），在内存中派生后再分页（往来单位量级有限，见 design.md §5）
        var partners = await partnerQuery
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.Type, p.PaymentTermDays })
            .ToListAsync(cancellationToken);

        if (partners.Count == 0)
        {
            return (Array.Empty<ReconciliationItem>(), 0);
        }

        var ids = partners.Select(p => p.Id).ToList();

        // 四类单据按往来聚合：未结金额 + 未结单据数（未作废且未结金额 > 0）
        // 余额按被核销单据未结金额（TotalAmount − SettledAmount）归集，不引用收付款单类型 → 天然支持收款挂供应商
        var salesShipments = await _dbContext.SalesShipments.AsNoTracking()
            .Where(o => ids.Contains(o.PartnerId) && o.Status == OrderStatus.Normal)
            .GroupBy(o => o.PartnerId)
            .Select(g => new
            {
                PartnerId = g.Key,
                UnsettledAmount = g.Sum(o => o.TotalAmount - o.SettledAmount),
                UnsettledCount = g.Count(o => o.SettledAmount < o.TotalAmount),
            })
            .ToListAsync(cancellationToken);

        var salesReturns = await _dbContext.SalesReturns.AsNoTracking()
            .Where(r => ids.Contains(r.PartnerId) && r.Status == OrderStatus.Normal)
            .GroupBy(r => r.PartnerId)
            .Select(g => new
            {
                PartnerId = g.Key,
                UnsettledAmount = g.Sum(r => r.TotalAmount - r.SettledAmount),
                UnsettledCount = g.Count(r => r.SettledAmount < r.TotalAmount),
            })
            .ToListAsync(cancellationToken);

        var purchaseReceipts = await _dbContext.PurchaseReceipts.AsNoTracking()
            .Where(o => ids.Contains(o.PartnerId) && o.Status == OrderStatus.Normal)
            .GroupBy(o => o.PartnerId)
            .Select(g => new
            {
                PartnerId = g.Key,
                UnsettledAmount = g.Sum(o => o.TotalAmount - o.SettledAmount),
                UnsettledCount = g.Count(o => o.SettledAmount < o.TotalAmount),
            })
            .ToListAsync(cancellationToken);

        var purchaseReturns = await _dbContext.PurchaseReturns.AsNoTracking()
            .Where(r => ids.Contains(r.PartnerId) && r.Status == OrderStatus.Normal)
            .GroupBy(r => r.PartnerId)
            .Select(g => new
            {
                PartnerId = g.Key,
                UnsettledAmount = g.Sum(r => r.TotalAmount - r.SettledAmount),
                UnsettledCount = g.Count(r => r.SettledAmount < r.TotalAmount),
            })
            .ToListAsync(cancellationToken);

        var salesShipmentMap = salesShipments.ToDictionary(x => x.PartnerId);
        var salesReturnMap = salesReturns.ToDictionary(x => x.PartnerId);
        var purchaseReceiptMap = purchaseReceipts.ToDictionary(x => x.PartnerId);
        var purchaseReturnMap = purchaseReturns.ToDictionary(x => x.PartnerId);

        // 应收侧未结单据的业务日期（§0.3：到期日 = 单据日期 + 账期天数；账期在内存叠加）
        // 已结清单据不参与逾期判定 → 查询条件带 SettledAmount < TotalAmount
        var salesShipmentDueDates = await _dbContext.SalesShipments.AsNoTracking()
            .Where(o => ids.Contains(o.PartnerId) && o.Status == OrderStatus.Normal && o.SettledAmount < o.TotalAmount)
            .Select(o => new { o.PartnerId, OrderDate = o.OrderDate })
            .ToListAsync(cancellationToken);

        var purchaseReturnDueDates = await _dbContext.PurchaseReturns.AsNoTracking()
            .Where(r => ids.Contains(r.PartnerId) && r.Status == OrderStatus.Normal && r.SettledAmount < r.TotalAmount)
            .Select(r => new { r.PartnerId, OrderDate = r.ReturnDate })
            .ToListAsync(cancellationToken);

        var dueDateMap = salesShipmentDueDates.Concat(purchaseReturnDueDates)
            .GroupBy(x => x.PartnerId)
            .ToDictionary(g => g.Key, g => g.Select(x => DateOnly.FromDateTime(x.OrderDate.UtcDateTime)).ToList());

        var allItems = partners.Select(p =>
        {
            // 应收 = 销售出库单未结 + 采购退货单未结；应付 = 采购入库单未结 + 销售退货单未结
            var receivableAmount =
                (salesShipmentMap.TryGetValue(p.Id, out var so) ? so.UnsettledAmount : 0m)
                + (purchaseReturnMap.TryGetValue(p.Id, out var pr) ? pr.UnsettledAmount : 0m);
            var payableAmount =
                (purchaseReceiptMap.TryGetValue(p.Id, out var po) ? po.UnsettledAmount : 0m)
                + (salesReturnMap.TryGetValue(p.Id, out var sr) ? sr.UnsettledAmount : 0m);

            var unsettledCount =
                (salesShipmentMap.TryGetValue(p.Id, out var so2) ? so2.UnsettledCount : 0)
                + (salesReturnMap.TryGetValue(p.Id, out var sr2) ? sr2.UnsettledCount : 0)
                + (purchaseReceiptMap.TryGetValue(p.Id, out var po2) ? po2.UnsettledCount : 0)
                + (purchaseReturnMap.TryGetValue(p.Id, out var pr2) ? pr2.UnsettledCount : 0);

            // 到期日 / 逾期派生（§0.3）：到期日 = 单据日期 + 账期天数；逾期 = 到期日已过且该单据未结清
            DateOnly? earliestDueDate = null;
            var maxOverdueDays = 0;
            var overdueCount = 0;
            if (dueDateMap.TryGetValue(p.Id, out var orderDates))
            {
                var dueDates = orderDates.Select(d => d.AddDays(p.PaymentTermDays)).ToList();
                earliestDueDate = dueDates.Min();
                foreach (var dueDate in dueDates)
                {
                    var overdueDays = today.DayNumber - dueDate.DayNumber;
                    if (overdueDays > 0)
                    {
                        overdueCount++;
                        maxOverdueDays = Math.Max(maxOverdueDays, overdueDays);
                    }
                }
            }

            return new ReconciliationItem
            {
                PartnerId = p.Id,
                PartnerName = p.Name,
                PartnerType = p.Type,
                ReceivableAmount = receivableAmount,
                PayableAmount = payableAmount,
                UnsettledOrderCount = unsettledCount,
                PaymentTermDays = p.PaymentTermDays,
                EarliestDueDate = earliestDueDate,
                MaxOverdueDays = maxOverdueDays,
                OverdueOrderCount = overdueCount,
            };
        })
            .Where(x => !overdueOnly || x.OverdueOrderCount > 0)
            .OrderBy(x => x.PartnerName)
            .ToList();

        var total = allItems.Count;
        var items = allItems.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return (items, total);
    }
}