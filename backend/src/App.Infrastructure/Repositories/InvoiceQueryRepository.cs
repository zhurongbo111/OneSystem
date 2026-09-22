using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 发票跨表只读查询仓储的 EF Core 实现（PostgreSQL）：可开票单据候选 + 已开票金额聚合。
/// 只读、不做业务判定；未开票候选在内存合并分页（单个往来的未开票单据量小，见 design.md §5）。
/// 「已开票金额」按 <c>InvoiceItems</c> 聚合推导（所属发票未作废才计入），不落单据列。
/// </summary>
public sealed class InvoiceQueryRepository : IInvoiceQueryRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化发票跨表查询仓储
    /// </summary>
    public InvoiceQueryRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<InvoicableOrderItem> Items, int Total)> GetInvoicableAsync(
        Guid partnerId,
        InvoiceType type,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var candidates = new List<InvoicableOrderItem>();

        if (type == InvoiceType.Purchase)
        {
            // 进项方向：采购入库单 + 采购退货单
            var receipts = await _dbContext.PurchaseReceipts.AsNoTracking()
                .Where(o => o.PartnerId == partnerId && o.Status == OrderStatus.Normal)
                .Select(o => new { o.Id, OrderNo = o.ReceiptNo, o.OrderDate, o.TotalAmount })
                .ToListAsync(cancellationToken);
            var receiptInvoiced = await GetInvoicedAmountsAsync(
                SettlementOrderType.PurchaseInbound, receipts.Select(o => o.Id).ToList(), cancellationToken);
            candidates.AddRange(receipts.Select(o => new InvoicableOrderItem
            {
                OrderType = SettlementOrderType.PurchaseInbound,
                OrderId = o.Id,
                OrderNo = o.OrderNo,
                OrderDate = o.OrderDate,
                TotalAmount = o.TotalAmount,
                InvoicedAmount = receiptInvoiced.GetValueOrDefault(o.Id),
            }));

            var purchaseReturns = await _dbContext.PurchaseReturns.AsNoTracking()
                .Where(r => r.PartnerId == partnerId && r.Status == OrderStatus.Normal)
                .Select(r => new { r.Id, OrderNo = r.ReturnNo, OrderDate = r.ReturnDate, r.TotalAmount })
                .ToListAsync(cancellationToken);
            var purchaseReturnInvoiced = await GetInvoicedAmountsAsync(
                SettlementOrderType.PurchaseReturn, purchaseReturns.Select(r => r.Id).ToList(), cancellationToken);
            candidates.AddRange(purchaseReturns.Select(r => new InvoicableOrderItem
            {
                OrderType = SettlementOrderType.PurchaseReturn,
                OrderId = r.Id,
                OrderNo = r.OrderNo,
                OrderDate = r.OrderDate,
                TotalAmount = r.TotalAmount,
                InvoicedAmount = purchaseReturnInvoiced.GetValueOrDefault(r.Id),
            }));
        }
        else
        {
            // 销项方向：销售出库单 + 销售退货单
            var shipments = await _dbContext.SalesShipments.AsNoTracking()
                .Where(o => o.PartnerId == partnerId && o.Status == OrderStatus.Normal)
                .Select(o => new { o.Id, OrderNo = o.ShipmentNo, o.OrderDate, o.TotalAmount })
                .ToListAsync(cancellationToken);
            var shipmentInvoiced = await GetInvoicedAmountsAsync(
                SettlementOrderType.SalesOutbound, shipments.Select(o => o.Id).ToList(), cancellationToken);
            candidates.AddRange(shipments.Select(o => new InvoicableOrderItem
            {
                OrderType = SettlementOrderType.SalesOutbound,
                OrderId = o.Id,
                OrderNo = o.OrderNo,
                OrderDate = o.OrderDate,
                TotalAmount = o.TotalAmount,
                InvoicedAmount = shipmentInvoiced.GetValueOrDefault(o.Id),
            }));

            var salesReturns = await _dbContext.SalesReturns.AsNoTracking()
                .Where(r => r.PartnerId == partnerId && r.Status == OrderStatus.Normal)
                .Select(r => new { r.Id, OrderNo = r.ReturnNo, OrderDate = r.ReturnDate, r.TotalAmount })
                .ToListAsync(cancellationToken);
            var salesReturnInvoiced = await GetInvoicedAmountsAsync(
                SettlementOrderType.SalesReturn, salesReturns.Select(r => r.Id).ToList(), cancellationToken);
            candidates.AddRange(salesReturns.Select(r => new InvoicableOrderItem
            {
                OrderType = SettlementOrderType.SalesReturn,
                OrderId = r.Id,
                OrderNo = r.OrderNo,
                OrderDate = r.OrderDate,
                TotalAmount = r.TotalAmount,
                InvoicedAmount = salesReturnInvoiced.GetValueOrDefault(r.Id),
            }));
        }

        // 只保留未开票金额 > 0 的单据（口径见 design.md §0.3），按业务日期倒序
        var available = candidates
            .Where(c => c.UninvoicedAmount > 0)
            .OrderByDescending(c => c.OrderDate)
            .ThenByDescending(c => c.OrderNo)
            .ToList();

        var total = available.Count;
        var items = available
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<decimal> GetInvoicedAmountAsync(SettlementOrderType orderType, Guid orderId, CancellationToken cancellationToken = default)
    {
        // 所属发票未作废才计入（作废即自动释放，见 design.md §0.3）
        var amount = await _dbContext.InvoiceItems.AsNoTracking()
            .Where(i => i.OrderType == orderType
                && i.OrderId == orderId
                && _dbContext.Invoices.Any(v => v.Id == i.InvoiceId && v.Status == OrderStatus.Normal))
            .SumAsync(i => (decimal?)i.Amount, cancellationToken);

        return amount ?? 0m;
    }

    /// <summary>
    /// 批量查询一组单据的已开票金额（按单据 id 归集），避免候选列表逐单查询 N+1
    /// </summary>
    private async Task<Dictionary<Guid, decimal>> GetInvoicedAmountsAsync(
        SettlementOrderType orderType,
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.InvoiceItems.AsNoTracking()
            .Where(i => i.OrderType == orderType
                && orderIds.Contains(i.OrderId)
                && _dbContext.Invoices.Any(v => v.Id == i.InvoiceId && v.Status == OrderStatus.Normal))
            .GroupBy(i => i.OrderId)
            .Select(g => new { OrderId = g.Key, Amount = g.Sum(i => i.Amount) })
            .ToDictionaryAsync(x => x.OrderId, x => x.Amount, cancellationToken);
    }
}