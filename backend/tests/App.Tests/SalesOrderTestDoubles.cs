using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Tests;

/// <summary>
/// 行为型销售订单仓储假实现（内存存储 + 记录调用）。
/// 规避 InMemory 提供程序不支持 <c>ExecuteUpdateAsync</c> 的关系型限制（见 design.md §6：Mock 仓储接口）。
/// 可选注入一个共享 <see cref="List{T}"/> 记录跨仓储的调用序列。
/// </summary>
internal sealed class FakeSalesOrderRepository : ISalesOrderRepository
{
    private readonly Dictionary<Guid, SalesOrder> _orders = [];
    private readonly Dictionary<Guid, List<SalesOrderItem>> _items = [];
    private readonly List<string>? _calls;

    public FakeSalesOrderRepository(List<string>? calls = null) => _calls = calls;

    /// <summary>新增写失败注入：返回非 null 异常时 AddAsync 抛出（模拟数据库写入失败）</summary>
    public Func<Exception?>? AddFailure { get; set; }

    /// <summary>更新写失败注入：返回非 null 异常时 UpdateAsync 抛出</summary>
    public Func<Exception?>? UpdateFailure { get; set; }

    /// <summary>已执行的累计量累加序列（明细行 id, delta），用于断言关联回写 / 作废回退</summary>
    public List<(Guid OrderItemId, int Delta)> FulfilledAdds { get; } = [];

    /// <summary>已执行的流转状态更新序列，用于断言状态推导</summary>
    public List<OrderFlowStatus> FlowStatusUpdates { get; } = [];

    /// <summary>分页查询注入结果（未设置时返回空页）</summary>
    public (IReadOnlyList<(SalesOrder Order, int UnfulfilledQuantity)> Items, int Total)? PagedResult { get; set; }

    /// <summary>最近一次分页查询入参，用于断言筛选透传</summary>
    public (string? Keyword, Guid? PartnerId, OrderFlowStatus? FlowStatus, DateTimeOffset? Start, DateTimeOffset? End, int Page, int PageSize)? LastPagedArgs { get; set; }

    /// <summary>已存订单（供断言）</summary>
    public IReadOnlyCollection<SalesOrder> Orders => _orders.Values;

    /// <summary>预置一张订单（供更新 / 作废 / 关闭 / 详情用例）</summary>
    public void Seed(SalesOrder order, IReadOnlyList<SalesOrderItem> items)
    {
        _orders[order.Id] = order;
        _items[order.Id] = items.ToList();
    }

    /// <summary>读取订单当前明细（供断言累计量回写结果）</summary>
    public IReadOnlyList<SalesOrderItem> ItemsOf(Guid orderId)
        => _items.TryGetValue(orderId, out var list) ? list : Array.Empty<SalesOrderItem>();

    public Task<(IReadOnlyList<(SalesOrder Order, int UnfulfilledQuantity)> Items, int Total)> GetPagedAsync(
        string? keyword, Guid? partnerId, OrderFlowStatus? flowStatus, DateTimeOffset? start, DateTimeOffset? end,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        _calls?.Add("GetPaged");
        LastPagedArgs = (keyword, partnerId, flowStatus, start, end, page, pageSize);
        if (PagedResult is not null)
        {
            return Task.FromResult(PagedResult.Value);
        }

        IReadOnlyList<(SalesOrder Order, int UnfulfilledQuantity)> empty = Array.Empty<(SalesOrder, int)>();
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

    public Task<(SalesOrder? Order, IReadOnlyList<SalesOrderItem> Items)> GetLinesAsync(Guid orderId, CancellationToken cancellationToken = default)
        => GetDetailAsync(orderId, cancellationToken);

    public Task<IReadOnlyList<SalesOrder>> GetPicksAsync(Guid partnerId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SalesOrder> picks = _orders.Values
            .Where(o => o.PartnerId == partnerId
                && (o.FlowStatus == OrderFlowStatus.Pending || o.FlowStatus == OrderFlowStatus.Partial))
            .OrderByDescending(o => o.CreatedAt)
            .ToList();
        return Task.FromResult(picks);
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

    public Task UpdateAsync(SalesOrder order, IReadOnlyList<SalesOrderItem> items, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Update");
        var failure = UpdateFailure?.Invoke();
        if (failure is not null)
        {
            throw failure;
        }

        _orders[order.Id] = order;
        _items[order.Id] = items.ToList(); // 明细全量替换
        return Task.CompletedTask;
    }

    public Task AddFulfilledQuantityAsync(Guid orderItemId, int delta, CancellationToken cancellationToken = default)
    {
        _calls?.Add("AddFulfilled");
        FulfilledAdds.Add((orderItemId, delta));
        foreach (var list in _items.Values)
        {
            var item = list.FirstOrDefault(i => i.Id == orderItemId);
            if (item is not null)
            {
                item.FulfilledQuantity += delta;
            }
        }

        return Task.CompletedTask;
    }

    public Task UpdateFlowStatusAsync(Guid id, OrderFlowStatus status, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        _calls?.Add("UpdateFlowStatus");
        FlowStatusUpdates.Add(status);
        var order = _orders[id];
        order.FlowStatus = status;
        order.UpdatedBy = operatorId;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<string> GenerateOrderNoAsync(string prefix, DateTimeOffset orderDate, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Generate");
        var pattern = $"{prefix}{orderDate.UtcDateTime:yyyyMMdd}";
        // 与真实仓储一致：序号 = 已存订单中前缀（前缀 + yyyyMMdd）匹配数 + 1
        var seq = _orders.Values.Count(o => o.OrderNo.StartsWith(pattern)) + 1;
        return Task.FromResult($"{pattern}{seq:D4}");
    }
}
