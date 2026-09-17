using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 报表跨表只读查询仓储的 EF Core 实现（PostgreSQL）：进销存报表 / 库存余额表 / 采购汇总 / 销售汇总。
/// 纯只读、不做业务判定；聚合在数据库侧完成（投影 + GroupBy），
/// 仅「期初与区间两段按商品合并」「分类维度排序分页」在内存拼接（单组织量级，见 specs/025-erp-report/design.md §1）。
/// 报表口径正文见 specs/025-erp-report/design.md §0.1。
/// </summary>
public sealed class ReportQueryRepository : IReportQueryRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化报表跨表查询仓储
    /// </summary>
    /// <param name="dbContext">EF Core 上下文</param>
    public ReportQueryRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<InventoryFlowItem> Items, int Total, InventoryFlowTotal Summary)> GetInventoryFlowAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? productId,
        Guid? categoryId,
        bool onlyChanged,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // 基准：启用商品（停用商品不再跟踪，与库存查询页口径一致），联查分类带出分类名
        var productQuery = from p in _dbContext.Products.AsNoTracking()
                           where p.Status == ProductStatus.Enabled
                           join c in _dbContext.Categories.AsNoTracking() on p.CategoryId equals c.Id
                           select new { p.Id, p.CategoryId, p.Code, p.Name, p.Unit, CategoryName = c.Name };

        if (productId is not null)
        {
            var value = productId.Value;
            productQuery = productQuery.Where(x => x.Id == value);
        }

        if (categoryId is not null)
        {
            var value = categoryId.Value;
            productQuery = productQuery.Where(x => x.CategoryId == value);
        }

        // 期初段：期间起点之前的**全部**流水累计（含全部变动类型，保证期末与库存台账同源可对账）
        var openingRows = await _dbContext.StockMovements.AsNoTracking()
            .Where(m => m.CreatedAt < start)
            .GroupBy(m => m.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(m => m.Quantity) })
            .ToListAsync(cancellationToken);

        var openingMap = new Dictionary<Guid, int>(openingRows.Count);
        foreach (var row in openingRows)
        {
            openingMap[row.ProductId] = row.Quantity;
        }

        // 区间段：按变动类型归入入 / 出；盘点调整为双向类型，按符号拆分（正计入、负计入出），不整条计入一侧
        var periodRows = await _dbContext.StockMovements.AsNoTracking()
            .Where(m => m.CreatedAt >= start && m.CreatedAt < end)
            .GroupBy(m => m.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Inbound = g.Sum(m => m.Quantity > 0
                    && (m.MovementType == StockMovementType.PurchaseInbound
                        || m.MovementType == StockMovementType.InitialStock
                        || m.MovementType == StockMovementType.PurchaseReturnVoid
                        || m.MovementType == StockMovementType.SalesReturnIn
                        || m.MovementType == StockMovementType.StockTakeAdjust)
                    ? m.Quantity
                    : 0),
                Outbound = g.Sum(m => m.Quantity < 0
                    && (m.MovementType == StockMovementType.PurchaseVoid
                        || m.MovementType == StockMovementType.SalesOutbound
                        || m.MovementType == StockMovementType.PurchaseReturnOut
                        || m.MovementType == StockMovementType.SalesReturnVoid
                        || m.MovementType == StockMovementType.StockTakeAdjust)
                    ? -m.Quantity
                    : 0),
            })
            .ToListAsync(cancellationToken);

        var periodMap = new Dictionary<Guid, (int Inbound, int Outbound)>(periodRows.Count);
        foreach (var row in periodRows)
        {
            periodMap[row.ProductId] = (row.Inbound, row.Outbound);
        }

        var products = await productQuery.ToListAsync(cancellationToken);

        var rows = new List<InventoryFlowItem>(products.Count);
        foreach (var product in products)
        {
            var opening = openingMap.TryGetValue(product.Id, out var openingQuantity) ? openingQuantity : 0;
            var inbound = 0;
            var outbound = 0;
            if (periodMap.TryGetValue(product.Id, out var period))
            {
                inbound = period.Inbound;
                outbound = period.Outbound;
            }

            // 只看有变动：期间入 / 出同时为 0 的商品（含仅有期初余额而无期间变动者）不展示
            if (onlyChanged && inbound == 0 && outbound == 0)
            {
                continue;
            }

            rows.Add(new InventoryFlowItem
            {
                ProductId = product.Id,
                Code = product.Code,
                Name = product.Name,
                CategoryName = product.CategoryName,
                Unit = product.Unit,
                OpeningQuantity = opening,
                InboundQuantity = inbound,
                OutboundQuantity = outbound,
                ClosingQuantity = opening + inbound - outbound,
            });
        }

        // 合计为全量筛选结果口径（在分页前聚合，避免只看当前页产生误读）
        var summary = new InventoryFlowTotal
        {
            OpeningQuantity = rows.Sum(r => r.OpeningQuantity),
            InboundQuantity = rows.Sum(r => r.InboundQuantity),
            OutboundQuantity = rows.Sum(r => r.OutboundQuantity),
            ClosingQuantity = rows.Sum(r => r.ClosingQuantity),
        };

        var ordered = rows.OrderBy(r => r.Code, StringComparer.Ordinal).ToList();
        var total = ordered.Count;
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return (items, total, summary);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<StockBalanceItem> Items, int Total, StockBalanceTotal Summary)> GetStockBalanceAsync(
        string? keyword,
        Guid? categoryId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // 基准：启用商品左连接库存台账（无库存行按 0 计），联查分类带出分类名
        var query = from p in _dbContext.Products.AsNoTracking()
                    where p.Status == ProductStatus.Enabled
                    join c in _dbContext.Categories.AsNoTracking() on p.CategoryId equals c.Id
                    join i in _dbContext.Inventory.AsNoTracking() on p.Id equals i.ProductId into iGroup
                    from i in iGroup.DefaultIfEmpty()
                    select new
                    {
                        p.CategoryId,
                        CategoryName = c.Name,
                        p.Code,
                        p.Name,
                        p.SafetyStock,
                        Quantity = i == null ? 0 : i.Quantity,
                    };

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(x => x.Code.ToLower().Contains(lower) || x.Name.ToLower().Contains(lower));
        }

        if (categoryId is not null)
        {
            var value = categoryId.Value;
            query = query.Where(x => x.CategoryId == value);
        }

        var grouped = await query
            .GroupBy(x => new { x.CategoryId, x.CategoryName })
            .Select(g => new StockBalanceItem
            {
                CategoryId = g.Key.CategoryId,
                CategoryName = g.Key.CategoryName,
                ProductCount = g.Count(),
                TotalQuantity = g.Sum(x => x.Quantity),
                ZeroStockCount = g.Count(x => x.Quantity == 0),
                // 低库存口径与库存查询页同源：安全阈值 > 0（0 表示不提醒）且库存 < 阈值
                BelowSafetyCount = g.Count(x => x.SafetyStock > 0 && x.Quantity < x.SafetyStock),
            })
            .ToListAsync(cancellationToken);

        var summary = new StockBalanceTotal
        {
            ProductCount = grouped.Sum(x => x.ProductCount),
            TotalQuantity = grouped.Sum(x => x.TotalQuantity),
            ZeroStockCount = grouped.Sum(x => x.ZeroStockCount),
            BelowSafetyCount = grouped.Sum(x => x.BelowSafetyCount),
        };

        var ordered = grouped.OrderBy(x => x.CategoryName, StringComparer.Ordinal).ToList();
        var total = ordered.Count;
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return (items, total, summary);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<PurchaseSummaryItem> Items, int Total, PurchaseSummaryTotal Summary)> GetPurchaseSummaryAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? partnerId,
        bool groupByProduct,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // 期间内未作废单据（作废即视为业务未发生，与库存回冲一致）
        var receiptQuery = _dbContext.PurchaseReceipts.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Normal && o.OrderDate >= start && o.OrderDate < end);
        var returnQuery = _dbContext.PurchaseReturns.AsNoTracking()
            .Where(r => r.Status == OrderStatus.Normal && r.ReturnDate >= start && r.ReturnDate < end);

        if (partnerId is not null)
        {
            var value = partnerId.Value;
            receiptQuery = receiptQuery.Where(o => o.PartnerId == value);
            returnQuery = returnQuery.Where(r => r.PartnerId == value);
        }

        var accumulators = new Dictionary<Guid, SummaryAccumulator>();

        if (groupByProduct)
        {
            // 商品维度：入库 / 退货均按明细商品聚合（名称 / 单位取明细快照；金额取明细小计）
            var receiptRows = await (from o in receiptQuery
                                     join i in _dbContext.PurchaseReceiptItems.AsNoTracking() on o.Id equals i.ReceiptId
                                     group new { o, i } by new { i.ProductId, i.ProductName, i.Unit } into g
                                     select new
                                     {
                                         g.Key.ProductId,
                                         g.Key.ProductName,
                                         g.Key.Unit,
                                         OrderCount = g.Select(x => x.o.Id).Distinct().Count(),
                                         Quantity = g.Sum(x => x.i.Quantity),
                                         Amount = g.Sum(x => x.i.Subtotal),
                                     })
                .ToListAsync(cancellationToken);

            foreach (var row in receiptRows)
            {
                var accumulator = GetAccumulator(accumulators, row.ProductId, row.ProductName, row.Unit);
                accumulator.OrderCount += row.OrderCount;
                accumulator.MainQuantity += row.Quantity;
                accumulator.MainAmount += row.Amount;
            }

            var returnRows = await (from r in returnQuery
                                    join i in _dbContext.PurchaseReturnItems.AsNoTracking() on r.Id equals i.ReturnId
                                    group i by new { i.ProductId, i.ProductName, i.Unit } into g
                                    select new
                                    {
                                        g.Key.ProductId,
                                        g.Key.ProductName,
                                        g.Key.Unit,
                                        Quantity = g.Sum(x => x.Quantity),
                                        Amount = g.Sum(x => x.Subtotal),
                                    })
                .ToListAsync(cancellationToken);

            foreach (var row in returnRows)
            {
                var accumulator = GetAccumulator(accumulators, row.ProductId, row.ProductName, row.Unit);
                accumulator.ReturnQuantity += row.Quantity;
                accumulator.ReturnAmount += row.Amount;
            }
        }
        else
        {
            // 往来维度：入库按供应商聚合（单号数取单据去重计数），退货按供应商聚合，两侧取并集
            var receiptRows = await (from o in receiptQuery
                                     join i in _dbContext.PurchaseReceiptItems.AsNoTracking() on o.Id equals i.ReceiptId
                                     group new { o, i } by new { o.PartnerId, o.PartnerName } into g
                                     select new
                                     {
                                         g.Key.PartnerId,
                                         g.Key.PartnerName,
                                         OrderCount = g.Select(x => x.o.Id).Distinct().Count(),
                                         Quantity = g.Sum(x => x.i.Quantity),
                                         Amount = g.Sum(x => x.i.Subtotal),
                                     })
                .ToListAsync(cancellationToken);

            foreach (var row in receiptRows)
            {
                var accumulator = GetAccumulator(accumulators, row.PartnerId, row.PartnerName, null);
                accumulator.OrderCount += row.OrderCount;
                accumulator.MainQuantity += row.Quantity;
                accumulator.MainAmount += row.Amount;
            }

            var returnRows = await (from r in returnQuery
                                    join i in _dbContext.PurchaseReturnItems.AsNoTracking() on r.Id equals i.ReturnId
                                    group i by new { r.PartnerId, r.PartnerName } into g
                                    select new
                                    {
                                        g.Key.PartnerId,
                                        g.Key.PartnerName,
                                        Quantity = g.Sum(x => x.Quantity),
                                        Amount = g.Sum(x => x.Subtotal),
                                    })
                .ToListAsync(cancellationToken);

            foreach (var row in returnRows)
            {
                var accumulator = GetAccumulator(accumulators, row.PartnerId, row.PartnerName, null);
                accumulator.ReturnQuantity += row.Quantity;
                accumulator.ReturnAmount += row.Amount;
            }
        }

        var summary = new PurchaseSummaryTotal
        {
            OrderCount = accumulators.Values.Sum(x => x.OrderCount),
            InboundQuantity = accumulators.Values.Sum(x => x.MainQuantity),
            InboundAmount = accumulators.Values.Sum(x => x.MainAmount),
            ReturnQuantity = accumulators.Values.Sum(x => x.ReturnQuantity),
            ReturnAmount = accumulators.Values.Sum(x => x.ReturnAmount),
        };

        var all = accumulators
            .Select(pair => new PurchaseSummaryItem
            {
                Key = pair.Key,
                Name = pair.Value.Name,
                Unit = pair.Value.Unit,
                OrderCount = pair.Value.OrderCount,
                InboundQuantity = pair.Value.MainQuantity,
                InboundAmount = pair.Value.MainAmount,
                ReturnQuantity = pair.Value.ReturnQuantity,
                ReturnAmount = pair.Value.ReturnAmount,
            })
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToList();

        var total = all.Count;
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return (items, total, summary);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<SalesSummaryItem> Items, int Total, SalesSummaryTotal Summary)> GetSalesSummaryAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? partnerId,
        bool groupByProduct,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // 期间内未作废单据（口径同采购汇总，替换为销售出库单与销售退货单）
        var shipmentQuery = _dbContext.SalesShipments.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Normal && o.OrderDate >= start && o.OrderDate < end);
        var returnQuery = _dbContext.SalesReturns.AsNoTracking()
            .Where(r => r.Status == OrderStatus.Normal && r.ReturnDate >= start && r.ReturnDate < end);

        if (partnerId is not null)
        {
            var value = partnerId.Value;
            shipmentQuery = shipmentQuery.Where(o => o.PartnerId == value);
            returnQuery = returnQuery.Where(r => r.PartnerId == value);
        }

        var accumulators = new Dictionary<Guid, SummaryAccumulator>();

        if (groupByProduct)
        {
            var shipmentRows = await (from o in shipmentQuery
                                      join i in _dbContext.SalesShipmentItems.AsNoTracking() on o.Id equals i.ShipmentId
                                      group new { o, i } by new { i.ProductId, i.ProductName, i.Unit } into g
                                      select new
                                      {
                                          g.Key.ProductId,
                                          g.Key.ProductName,
                                          g.Key.Unit,
                                          OrderCount = g.Select(x => x.o.Id).Distinct().Count(),
                                          Quantity = g.Sum(x => x.i.Quantity),
                                          Amount = g.Sum(x => x.i.Subtotal),
                                      })
                .ToListAsync(cancellationToken);

            foreach (var row in shipmentRows)
            {
                var accumulator = GetAccumulator(accumulators, row.ProductId, row.ProductName, row.Unit);
                accumulator.OrderCount += row.OrderCount;
                accumulator.MainQuantity += row.Quantity;
                accumulator.MainAmount += row.Amount;
            }

            var returnRows = await (from r in returnQuery
                                    join i in _dbContext.SalesReturnItems.AsNoTracking() on r.Id equals i.ReturnId
                                    group i by new { i.ProductId, i.ProductName, i.Unit } into g
                                    select new
                                    {
                                        g.Key.ProductId,
                                        g.Key.ProductName,
                                        g.Key.Unit,
                                        Quantity = g.Sum(x => x.Quantity),
                                        Amount = g.Sum(x => x.Subtotal),
                                    })
                .ToListAsync(cancellationToken);

            foreach (var row in returnRows)
            {
                var accumulator = GetAccumulator(accumulators, row.ProductId, row.ProductName, row.Unit);
                accumulator.ReturnQuantity += row.Quantity;
                accumulator.ReturnAmount += row.Amount;
            }
        }
        else
        {
            var shipmentRows = await (from o in shipmentQuery
                                      join i in _dbContext.SalesShipmentItems.AsNoTracking() on o.Id equals i.ShipmentId
                                      group new { o, i } by new { o.PartnerId, o.PartnerName } into g
                                      select new
                                      {
                                          g.Key.PartnerId,
                                          g.Key.PartnerName,
                                          OrderCount = g.Select(x => x.o.Id).Distinct().Count(),
                                          Quantity = g.Sum(x => x.i.Quantity),
                                          Amount = g.Sum(x => x.i.Subtotal),
                                      })
                .ToListAsync(cancellationToken);

            foreach (var row in shipmentRows)
            {
                var accumulator = GetAccumulator(accumulators, row.PartnerId, row.PartnerName, null);
                accumulator.OrderCount += row.OrderCount;
                accumulator.MainQuantity += row.Quantity;
                accumulator.MainAmount += row.Amount;
            }

            var returnRows = await (from r in returnQuery
                                    join i in _dbContext.SalesReturnItems.AsNoTracking() on r.Id equals i.ReturnId
                                    group i by new { r.PartnerId, r.PartnerName } into g
                                    select new
                                    {
                                        g.Key.PartnerId,
                                        g.Key.PartnerName,
                                        Quantity = g.Sum(x => x.Quantity),
                                        Amount = g.Sum(x => x.Subtotal),
                                    })
                .ToListAsync(cancellationToken);

            foreach (var row in returnRows)
            {
                var accumulator = GetAccumulator(accumulators, row.PartnerId, row.PartnerName, null);
                accumulator.ReturnQuantity += row.Quantity;
                accumulator.ReturnAmount += row.Amount;
            }
        }

        var summary = new SalesSummaryTotal
        {
            OrderCount = accumulators.Values.Sum(x => x.OrderCount),
            OutboundQuantity = accumulators.Values.Sum(x => x.MainQuantity),
            OutboundAmount = accumulators.Values.Sum(x => x.MainAmount),
            ReturnQuantity = accumulators.Values.Sum(x => x.ReturnQuantity),
            ReturnAmount = accumulators.Values.Sum(x => x.ReturnAmount),
        };

        var all = accumulators
            .Select(pair => new SalesSummaryItem
            {
                Key = pair.Key,
                Name = pair.Value.Name,
                Unit = pair.Value.Unit,
                OrderCount = pair.Value.OrderCount,
                OutboundQuantity = pair.Value.MainQuantity,
                OutboundAmount = pair.Value.MainAmount,
                ReturnQuantity = pair.Value.ReturnQuantity,
                ReturnAmount = pair.Value.ReturnAmount,
            })
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToList();

        var total = all.Count;
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return (items, total, summary);
    }

    /// <summary>
    /// 取（或新建）分组累加器：入库 / 出库侧与退货侧按分组键合并，无一侧数据时该侧保持 0。
    /// </summary>
    private static SummaryAccumulator GetAccumulator(
        Dictionary<Guid, SummaryAccumulator> accumulators,
        Guid key,
        string name,
        string? unit)
    {
        if (!accumulators.TryGetValue(key, out var accumulator))
        {
            accumulator = new SummaryAccumulator { Name = name, Unit = unit };
            accumulators[key] = accumulator;
        }

        return accumulator;
    }

    /// <summary>
    /// 汇总行的分组累加器（采购 / 销售汇总共用：主侧为入库或出库，另一侧为退货）。
    /// </summary>
    private sealed class SummaryAccumulator
    {
        /// <summary>分组名称（往来单位名称或商品名称快照）</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>计量单位（仅商品维度有值）</summary>
        public string? Unit { get; set; }

        /// <summary>主侧单据数（入库 / 出库）</summary>
        public int OrderCount { get; set; }

        /// <summary>主侧数量（入库 / 出库）</summary>
        public int MainQuantity { get; set; }

        /// <summary>主侧金额</summary>
        public decimal MainAmount { get; set; }

        /// <summary>退货数量</summary>
        public int ReturnQuantity { get; set; }

        /// <summary>退货金额</summary>
        public decimal ReturnAmount { get; set; }
    }
}
