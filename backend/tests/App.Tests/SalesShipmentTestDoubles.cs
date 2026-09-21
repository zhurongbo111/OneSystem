using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Tests;

/// <summary>
/// 行为型销售单仓储假实现（内存存储 + 记录调用），与 <see cref="FakePurchaseReceiptRepository"/> 同构。
/// 规避 InMemory 提供程序不支持 <c>ExecuteUpdateAsync</c> 的关系型限制（见 design.md §6）。
/// 可选注入一个共享 <see cref="List{T}"/> 记录跨仓储的调用序列。
/// </summary>
internal sealed class FakeSalesShipmentRepository : ISalesShipmentRepository
{
    private readonly Dictionary<Guid, SalesShipment> _orders = [];
    private readonly Dictionary<Guid, List<SalesShipmentItem>> _items = [];
    private readonly List<string>? _calls;

    public FakeSalesShipmentRepository(List<string>? calls = null) => _calls = calls;

    /// <summary>新增写失败注入：返回非 null 异常时 AddAsync 抛出（模拟数据库写入失败）</summary>
    public Func<Exception?>? AddFailure { get; set; }

    /// <summary>已存单据（供断言）</summary>
    public IReadOnlyCollection<SalesShipment> Orders => _orders.Values;

    /// <summary>预置一张单据（供 Void / Settlement / GetById 用例）</summary>
    public void Seed(SalesShipment order, IReadOnlyList<SalesShipmentItem> items)
    {
        _orders[order.Id] = order;
        _items[order.Id] = items.ToList();
    }

    /// <summary>已执行的分页查询入参</summary>
    public List<(string? Keyword, Guid? PartnerId, Guid? OrderId, DateTimeOffset? Start, DateTimeOffset? End,
        SettlementState? SettlementState, int Page, int PageSize)> PagedQueries
    { get; } = [];

    /// <summary>分页查询返回的行（由用例预置）</summary>
    public IReadOnlyList<(SalesShipment Order, int TotalQuantity)> PagedItems { get; set; }
        = Array.Empty<(SalesShipment, int)>();

    /// <summary>分页查询返回的总数（由用例预置）</summary>
    public int PagedTotal { get; set; }

    public Task<(IReadOnlyList<(SalesShipment Order, int TotalQuantity)> Items, int Total)> GetPagedAsync(
        string? keyword, Guid? partnerId, Guid? orderId, DateTimeOffset? start, DateTimeOffset? end,
        SettlementState? settlementState, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        PagedQueries.Add((keyword, partnerId, orderId, start, end, settlementState, page, pageSize));
        return Task.FromResult((PagedItems, PagedTotal));
    }

    /// <summary>已执行的详情查询入参（id, includeItems），供断言取数范围（见 erp-settlement design.md §3.1.1）</summary>
    public List<(Guid Id, bool IncludeItems)> DetailQueries { get; } = [];

    public Task<(SalesShipment? Order, IReadOnlyList<SalesShipmentItem> Items)> GetDetailAsync(Guid id, bool includeItems = true, CancellationToken cancellationToken = default)
    {
        DetailQueries.Add((id, includeItems));

        if (!_orders.TryGetValue(id, out var order))
        {
            return Task.FromResult<(SalesShipment?, IReadOnlyList<SalesShipmentItem>)>((null, Array.Empty<SalesShipmentItem>()));
        }

        // 与真实仓储一致：includeItems = false 时只取主表、不返回明细（Items 恒为空集合）
        IReadOnlyList<SalesShipmentItem> items = includeItems
            ? _items[id].OrderBy(i => i.Id).ToList() // 按 Id 还原插入顺序
            : Array.Empty<SalesShipmentItem>();
        return Task.FromResult((Order: (SalesShipment?)order, Items: items));
    }

    /// <summary>批量取明细（erp-export 导出用）：与真实仓储同口径，按明细 Id 升序</summary>
    public Task<IReadOnlyList<SalesShipmentItem>> GetItemsByOrderIdsAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken = default)
    {
        _calls?.Add("GetItemsByOrderIds");
        IReadOnlyList<SalesShipmentItem> items = _items
            .Where(kv => orderIds.Contains(kv.Key))
            .SelectMany(kv => kv.Value)
            .OrderBy(i => i.Id)
            .ToList();
        return Task.FromResult(items);
    }

    public Task AddAsync(SalesShipment order, IReadOnlyList<SalesShipmentItem> items, CancellationToken cancellationToken = default)
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
        var seq = _orders.Values.Count(o => o.ShipmentNo.StartsWith(pattern)) + 1;
        return Task.FromResult($"{pattern}{seq:D4}");
    }
}
