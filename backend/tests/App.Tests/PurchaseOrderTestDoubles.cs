using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Tests;

/// <summary>
/// 行为型采购单仓储假实现（内存存储 + 记录调用）。
/// 规避 InMemory 提供程序不支持 <c>ExecuteUpdateAsync</c> 的关系型限制（见 design.md §6：Mock 仓储接口）。
/// 可选注入一个共享 <see cref="List{T}"/> 记录跨仓储的调用序列。
/// </summary>
internal sealed class FakePurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly Dictionary<Guid, PurchaseOrder> _orders = new();
    private readonly Dictionary<Guid, List<PurchaseOrderItem>> _items = new();
    private readonly List<string>? _calls;

    public FakePurchaseOrderRepository(List<string>? calls = null) => _calls = calls;

    /// <summary>新增写失败注入：返回非 null 异常时 AddAsync 抛出（模拟数据库写入失败）</summary>
    public Func<Exception?>? AddFailure { get; set; }

    /// <summary>预置一张单据（供 Void / Settlement / GetById 用例）</summary>
    public void Seed(PurchaseOrder order, IReadOnlyList<PurchaseOrderItem> items)
    {
        _orders[order.Id] = order;
        _items[order.Id] = items.ToList();
    }

    public Task<(IReadOnlyList<PurchaseOrder> Items, int Total)> GetPagedAsync(
        string? keyword, Guid? partnerId, DateTimeOffset? start, DateTimeOffset? end,
        SettlementState? settlementState, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PurchaseOrder> empty = Array.Empty<PurchaseOrder>();
        return Task.FromResult((empty, 0));
    }

    public Task<(PurchaseOrder? Order, IReadOnlyList<PurchaseOrderItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_orders.TryGetValue(id, out var order))
        {
            return Task.FromResult<(PurchaseOrder?, IReadOnlyList<PurchaseOrderItem>)>((null, Array.Empty<PurchaseOrderItem>()));
        }

        IReadOnlyList<PurchaseOrderItem> items = _items[id].OrderBy(i => i.Id).ToList(); // 与真实仓储一致：按 Id 还原插入顺序
        return Task.FromResult((Order: (PurchaseOrder?)order, Items: items));
    }

    public Task AddAsync(PurchaseOrder order, IReadOnlyList<PurchaseOrderItem> items, CancellationToken cancellationToken = default)
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
        var seq = _orders.Values.Count(o => o.OrderNo.StartsWith(pattern)) + 1;
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
