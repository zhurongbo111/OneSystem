using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Tests;

/// <summary>
/// 行为型采购单仓储假实现（内存存储 + 记录调用）。
/// 规避 InMemory 提供程序不支持 <c>ExecuteUpdateAsync</c> 的关系型限制（见 design.md §6：Mock 仓储接口）。
/// 可选注入一个共享 <see cref="List{T}"/> 记录跨仓储的调用序列。
/// </summary>
internal sealed class FakePurchaseReceiptRepository : IPurchaseReceiptRepository
{
    private readonly Dictionary<Guid, PurchaseReceipt> _orders = new();
    private readonly Dictionary<Guid, List<PurchaseReceiptItem>> _items = new();
    private readonly List<string>? _calls;

    public FakePurchaseReceiptRepository(List<string>? calls = null) => _calls = calls;

    /// <summary>新增写失败注入：返回非 null 异常时 AddAsync 抛出（模拟数据库写入失败）</summary>
    public Func<Exception?>? AddFailure { get; set; }

    /// <summary>已存单据（供断言）</summary>
    public IReadOnlyCollection<PurchaseReceipt> Orders => _orders.Values;

    /// <summary>预置一张单据（供 Void / Settlement / GetById 用例）</summary>
    public void Seed(PurchaseReceipt order, IReadOnlyList<PurchaseReceiptItem> items)
    {
        _orders[order.Id] = order;
        _items[order.Id] = items.ToList();
    }

    /// <summary>已执行的分页查询入参</summary>
    public List<(string? Keyword, Guid? PartnerId, Guid? OrderId, DateTimeOffset? Start, DateTimeOffset? End,
        SettlementState? SettlementState, int Page, int PageSize)> PagedQueries { get; } = new();

    /// <summary>分页查询返回的行（由用例预置）</summary>
    public IReadOnlyList<(PurchaseReceipt Order, int TotalQuantity)> PagedItems { get; set; }
        = Array.Empty<(PurchaseReceipt, int)>();

    /// <summary>分页查询返回的总数（由用例预置）</summary>
    public int PagedTotal { get; set; }

    public Task<(IReadOnlyList<(PurchaseReceipt Order, int TotalQuantity)> Items, int Total)> GetPagedAsync(
        string? keyword, Guid? partnerId, Guid? orderId, DateTimeOffset? start, DateTimeOffset? end,
        SettlementState? settlementState, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        PagedQueries.Add((keyword, partnerId, orderId, start, end, settlementState, page, pageSize));
        return Task.FromResult((PagedItems, PagedTotal));
    }

    public Task<(PurchaseReceipt? Order, IReadOnlyList<PurchaseReceiptItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_orders.TryGetValue(id, out var order))
        {
            return Task.FromResult<(PurchaseReceipt?, IReadOnlyList<PurchaseReceiptItem>)>((null, Array.Empty<PurchaseReceiptItem>()));
        }

        IReadOnlyList<PurchaseReceiptItem> items = _items[id].OrderBy(i => i.Id).ToList(); // 与真实仓储一致：按 Id 还原插入顺序
        return Task.FromResult((Order: (PurchaseReceipt?)order, Items: items));
    }

    /// <summary>批量取明细（erp-export 导出用）：与真实仓储同口径，按明细 Id 升序</summary>
    public Task<IReadOnlyList<PurchaseReceiptItem>> GetItemsByOrderIdsAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken = default)
    {
        _calls?.Add("GetItemsByOrderIds");
        IReadOnlyList<PurchaseReceiptItem> items = _items
            .Where(kv => orderIds.Contains(kv.Key))
            .SelectMany(kv => kv.Value)
            .OrderBy(i => i.Id)
            .ToList();
        return Task.FromResult(items);
    }

    public Task AddAsync(PurchaseReceipt order, IReadOnlyList<PurchaseReceiptItem> items, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Add");
        var failure = AddFailure?.Invoke();
        if (failure is not null)
        {
            throw failure;
        }

        _orders[order.Id] = order;
        _items[order.Id] = items.ToList();
        return Task.CompletedTask;
    }

    public Task AddSettledAmountAsync(Guid id, decimal delta, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        _calls?.Add("AddSettledAmount");
        var order = _orders[id];
        order.SettledAmount += delta;
        order.UpdatedBy = operatorId;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(Guid id, OrderStatus status, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        _calls?.Add("UpdateStatus");
        var order = _orders[id];
        order.Status = status;
        order.UpdatedBy = operatorId;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<string> GenerateOrderNoAsync(string prefix, DateTimeOffset orderDate, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Generate");
        var pattern = $"{prefix}{orderDate.UtcDateTime:yyyyMMdd}";
        // 与真实仓储一致：序号 = 已存单据中前缀（前缀 + yyyyMMdd）匹配数 + 1
        var seq = _orders.Values.Count(o => o.ReceiptNo.StartsWith(pattern)) + 1;
        return Task.FromResult($"{pattern}{seq:D4}");
    }
}

/// <summary>
/// 行为型库存仓储假实现（内存台账 + 记录回冲/入库增量），规避 InMemory 不支持 <c>ExecuteUpdateAsync</c>。
/// </summary>
internal sealed class FakeInventoryRepository : IInventoryRepository
{
    private readonly Dictionary<Guid, int> _stock = new();
    private readonly List<string>? _calls;

    public FakeInventoryRepository(List<string>? calls = null) => _calls = calls;

    /// <summary>增量操作失败注入：返回非 null 异常时 IncrementAsync 抛出</summary>
    public Func<Exception?>? IncrementFailure { get; set; }

    /// <summary>扣减失败注入：命中返回 false 的商品 id 集合（模拟库存不足，TryDecrementAsync 返回 false）</summary>
    public HashSet<Guid> TryDecrementFailProducts { get; } = new();

    /// <summary>已执行的增量序列（productId, delta），用于断言回冲 / 入库调用</summary>
    public List<(Guid ProductId, int Delta)> Increments { get; } = new();

    /// <summary>已执行的扣减序列（productId, amount），用于断言销售扣减调用</summary>
    public List<(Guid ProductId, int Amount)> Decrements { get; } = new();

    /// <summary>设定值失败注入：返回非 null 异常时 SetQuantityAsync 抛出</summary>
    public Func<Exception?>? SetQuantityFailure { get; set; }

    /// <summary>已执行的设定序列（productId, quantity），用于断言库存校正调用</summary>
    public List<(Guid ProductId, int Quantity)> Sets { get; } = new();

    /// <summary>结存成本额台账（productId → CostAmount；erp-cost）</summary>
    public Dictionary<Guid, decimal> CostAmounts { get; } = new();

    /// <summary>移动加权平均单价台账（productId → AverageCost；erp-cost，派生值）</summary>
    public Dictionary<Guid, decimal> AverageCosts { get; } = new();

    /// <summary>入库成本调用序列（productId, quantity, unitCost），用于断言「成本与数量同事务」</summary>
    public List<(Guid ProductId, int Quantity, decimal UnitCost)> InboundCosts { get; } = new();

    /// <summary>出库成本结转序列（productId, totalCost）</summary>
    public List<(Guid ProductId, decimal TotalCost)> OutboundCosts { get; } = new();

    /// <summary>均价读取序列（productId），用于断言「出库前先读均价」发生</summary>
    public List<Guid> AverageCostReads { get; } = new();

    /// <summary>已读取过账面的商品 id 集合（断言「事务内读账面」发生）</summary>
    public HashSet<Guid> BookRead { get; } = new();

    public void Seed(Guid productId, int quantity) => _stock[productId] = quantity;

    public int GetQuantity(Guid productId) => _stock.GetValueOrDefault(productId);

    public Task AddAsync(Inventory inventory, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<int> GetQuantityAsync(Guid productId, CancellationToken cancellationToken = default)
        => Task.FromResult(GetQuantity(productId));

    public Task IncrementAsync(Guid productId, int delta, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Increment");
        var failure = IncrementFailure?.Invoke();
        if (failure is not null)
        {
            throw failure;
        }

        Increments.Add((productId, delta));
        _stock[productId] = _stock.GetValueOrDefault(productId) + delta;
        return Task.CompletedTask;
    }

    public Task<bool> TryDecrementAsync(Guid productId, int amount, CancellationToken cancellationToken = default)
    {
        _calls?.Add("TryDecrement");
        if (TryDecrementFailProducts.Contains(productId) || GetQuantity(productId) < amount)
        {
            return Task.FromResult(false);
        }

        Decrements.Add((productId, amount));
        _stock[productId] = GetQuantity(productId) - amount;
        return Task.FromResult(true);
    }

    public Task<int> SetQuantityAsync(Guid productId, int quantity, CancellationToken cancellationToken = default)
    {
        _calls?.Add("SetQuantity");
        BookRead.Add(productId);
        var failure = SetQuantityFailure?.Invoke();
        if (failure is not null)
        {
            throw failure;
        }

        Sets.Add((productId, quantity));
        _stock[productId] = quantity;
        return Task.FromResult(1);
    }

    public Task<decimal> GetAverageCostAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        _calls?.Add("GetAverageCost");
        AverageCostReads.Add(productId);
        return Task.FromResult(AverageCosts.GetValueOrDefault(productId));
    }

    public Task ApplyInboundCostAsync(
        Guid productId, int quantity, decimal unitCost, CancellationToken cancellationToken = default)
    {
        _calls?.Add("ApplyInboundCost");
        InboundCosts.Add((productId, quantity, unitCost));

        // 与真实仓储同口径：金额 += Round(数量 × 单价, 4)；均价 = 金额 ÷ 数量（数量为 0 时保留最后均价）
        var amount = CostAmounts.GetValueOrDefault(productId)
            + Math.Round(quantity * unitCost, 4, MidpointRounding.AwayFromZero);
        CostAmounts[productId] = amount;

        var qty = GetQuantity(productId);
        if (qty != 0)
        {
            AverageCosts[productId] = Math.Round(amount / qty, 4, MidpointRounding.AwayFromZero);
        }

        return Task.CompletedTask;
    }

    public Task ApplyOutboundCostAsync(
        Guid productId, decimal totalCost, CancellationToken cancellationToken = default)
    {
        _calls?.Add("ApplyOutboundCost");
        OutboundCosts.Add((productId, totalCost));

        // 出库不改变均价；数量归零时成本额归 0（消除尾差）
        var qty = GetQuantity(productId);
        CostAmounts[productId] = qty == 0 ? 0m : CostAmounts.GetValueOrDefault(productId) - totalCost;
        return Task.CompletedTask;
    }

    public Task SetCostAsync(
        Guid productId, decimal costAmount, decimal averageCost, CancellationToken cancellationToken = default)
    {
        _calls?.Add("SetCost");
        CostAmounts[productId] = costAmount;
        AverageCosts[productId] = averageCost;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<Guid, int>> GetQuantitiesAsync(
        IReadOnlyList<Guid> productIds, CancellationToken cancellationToken = default)
    {
        _calls?.Add("GetQuantities");
        foreach (var id in productIds)
        {
            BookRead.Add(id);
        }

        var dict = productIds.ToDictionary(id => id, id => GetQuantity(id));
        return Task.FromResult<IReadOnlyDictionary<Guid, int>>(dict);
    }

    public Task<(IReadOnlyList<InventoryItem> Items, int Total)> GetPagedAsync(
        string? keyword, Guid? categoryId, int page, int pageSize, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

/// <summary>
/// 记录调用序列的工作单元桩；可选注入 Commit 失败以触发 Handler 回滚分支。
/// </summary>
internal sealed class RecordingUnitOfWork : IUnitOfWork
{
    private readonly List<string>? _calls;

    public RecordingUnitOfWork(List<string>? calls = null) => _calls = calls;

    /// <summary>提交失败注入：返回非 null 异常时 CommitAsync 抛出</summary>
    public Func<Exception?>? CommitFailure { get; set; }

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _calls?.Add("Begin");
        return Task.CompletedTask;
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        _calls?.Add("Commit");
        var failure = CommitFailure?.Invoke();
        if (failure is not null)
        {
            throw failure;
        }
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        _calls?.Add("Rollback");
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
