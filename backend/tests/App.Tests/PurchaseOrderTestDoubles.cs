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

    public Task<(IReadOnlyList<PurchaseOrderListItem> Items, int Total)> GetPagedAsync(
        string? keyword, Guid? partnerId, DateTimeOffset? start, DateTimeOffset? end,
        OrderSettlementStatus? settlement, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PurchaseOrderListItem> empty = Array.Empty<PurchaseOrderListItem>();
        return Task.FromResult((empty, 0));
    }

    public Task<PurchaseOrderDetail?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_orders.TryGetValue(id, out var order))
        {
            return Task.FromResult<PurchaseOrderDetail?>(null);
        }

        var items = _items[id].OrderBy(i => i.Id).ToList(); // 与真实仓储一致：按 Id 还原插入顺序
        return Task.FromResult<PurchaseOrderDetail?>(ToDetail(order, items));
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

    public Task UpdateSettlementAsync(Guid id, OrderSettlementStatus settlement, CancellationToken cancellationToken = default)
    {
        _calls?.Add("UpdateSettlement");
        _orders[id].SettlementStatus = settlement;
        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(Guid id, OrderStatus status, CancellationToken cancellationToken = default)
    {
        _calls?.Add("UpdateStatus");
        _orders[id].Status = status;
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

    private static PurchaseOrderDetail ToDetail(PurchaseOrder order, IReadOnlyList<PurchaseOrderItem> items)
        => new()
        {
            Id = order.Id,
            OrderNo = order.OrderNo,
            PartnerId = order.PartnerId,
            PartnerName = order.PartnerName,
            OrderDate = order.OrderDate,
            TotalAmount = order.TotalAmount,
            SettlementStatus = order.SettlementStatus,
            Status = order.Status,
            Remark = order.Remark,
            CreatedBy = order.CreatedBy,
            CreatedAt = order.CreatedAt,
            Items = items.Select(i => new PurchaseOrderDetailItem
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Unit = i.Unit,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Subtotal = i.Subtotal,
            }).ToList(),
        };
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
