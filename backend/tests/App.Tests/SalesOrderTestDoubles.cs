using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Tests;

/// <summary>
/// 行为型销售单仓储假实现（内存存储 + 记录调用），与 <see cref="FakePurchaseOrderRepository"/> 同构。
/// 规避 InMemory 提供程序不支持 <c>ExecuteUpdateAsync</c> 的关系型限制（见 design.md §6）。
/// 可选注入一个共享 <see cref="List{T}"/> 记录跨仓储的调用序列。
/// </summary>
internal sealed class FakeSalesOrderRepository : ISalesOrderRepository
{
    private readonly Dictionary<Guid, SalesOrder> _orders = new();
    private readonly Dictionary<Guid, List<SalesOrderItem>> _items = new();
    private readonly List<string>? _calls;

    public FakeSalesOrderRepository(List<string>? calls = null) => _calls = calls;

    /// <summary>新增写失败注入：返回非 null 异常时 AddAsync 抛出（模拟数据库写入失败）</summary>
    public Func<Exception?>? AddFailure { get; set; }

    /// <summary>预置一张单据（供 Void / Settlement / GetById 用例）</summary>
    public void Seed(SalesOrder order, IReadOnlyList<SalesOrderItem> items)
    {
        _orders[order.Id] = order;
        _items[order.Id] = items.ToList();
    }

    public Task<(IReadOnlyList<SalesOrder> Items, int Total)> GetPagedAsync(
        string? keyword, Guid? partnerId, DateTimeOffset? start, DateTimeOffset? end,
        SettlementState? settlementState, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SalesOrder> empty = Array.Empty<SalesOrder>();
        return Task.FromResult((empty, 0));
    }

    public Task<(SalesOrder? Order, IReadOnlyList<SalesOrderItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_orders.TryGetValue(id, out var order))
        {
            return Task.FromResult<(SalesOrder?, IReadOnlyList<SalesOrderItem>)>((null, Array.Empty<SalesOrderItem>()));
        }

        IReadOnlyList<SalesOrderItem> items = _items[id].OrderBy(i => i.Id).ToList(); // 与真实仓储一致：按 Id 还原插入顺序
        return Task.FromResult((Order: (SalesOrder?)order, Items: items));
    }

    public Task AddAsync(SalesOrder order, IReadOnlyList<SalesOrderItem> items, CancellationToken cancellationToken = default)
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
