using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Tests;

/// <summary>
/// 行为型采购退货单仓储假实现（内存存储 + 记录调用与分页入参）。
/// 规避 InMemory 提供程序不支持 <c>ExecuteUpdateAsync</c> 的关系型限制（见 design.md §6）。
/// <see cref="PagedQueries"/> 记录分页筛选入参（供 GetPurchaseReturns 用例断言传参透传）。
/// </summary>
internal sealed class FakePurchaseReturnRepository : IPurchaseReturnRepository
{
    private readonly Dictionary<Guid, PurchaseReturn> _returns = new();
    private readonly Dictionary<Guid, List<PurchaseReturnItem>> _items = new();
    private readonly List<string>? _calls;

    public FakePurchaseReturnRepository(List<string>? calls = null) => _calls = calls;

    /// <summary>新增写失败注入：返回非 null 异常时 AddAsync 抛出（模拟数据库写入失败）</summary>
    public Func<Exception?>? AddFailure { get; set; }

    /// <summary>已执行的分页查询入参（keyword / partnerId / start / end / settlement / page / pageSize）</summary>
    public List<(string? Keyword, Guid? PartnerId, DateTimeOffset? Start, DateTimeOffset? End, OrderSettlementStatus? Settlement, int Page, int PageSize)> PagedQueries { get; } = new();

    /// <summary>分页查询返回的行（由用例预置）</summary>
    public IReadOnlyList<PurchaseReturn> PagedItems { get; set; } = Array.Empty<PurchaseReturn>();

    /// <summary>分页查询返回的总数（由用例预置）</summary>
    public int PagedTotal { get; set; }

    /// <summary>预置一张单据（供 Void / Settlement / GetById 用例）</summary>
    public void Seed(PurchaseReturn purchaseReturn, IReadOnlyList<PurchaseReturnItem> items)
    {
        _returns[purchaseReturn.Id] = purchaseReturn;
        _items[purchaseReturn.Id] = items.ToList();
    }

    public Task<(IReadOnlyList<PurchaseReturn> Items, int Total)> GetPagedAsync(
        string? keyword, Guid? partnerId, DateTimeOffset? start, DateTimeOffset? end,
        OrderSettlementStatus? settlement, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        PagedQueries.Add((keyword, partnerId, start, end, settlement, page, pageSize));
        return Task.FromResult((PagedItems, PagedTotal));
    }

    public Task<(PurchaseReturn? Return, IReadOnlyList<PurchaseReturnItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_returns.TryGetValue(id, out var purchaseReturn))
        {
            return Task.FromResult<(PurchaseReturn?, IReadOnlyList<PurchaseReturnItem>)>((null, Array.Empty<PurchaseReturnItem>()));
        }

        IReadOnlyList<PurchaseReturnItem> items = _items[id].OrderBy(i => i.Id).ToList(); // 与真实仓储一致：按 Id 还原插入顺序
        return Task.FromResult((Return: (PurchaseReturn?)purchaseReturn, Items: items));
    }

    public Task AddAsync(PurchaseReturn purchaseReturn, IReadOnlyList<PurchaseReturnItem> items, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Add");
        var failure = AddFailure?.Invoke();
        if (failure is not null)
        {
            throw failure;
        }

        _returns[purchaseReturn.Id] = purchaseReturn;
        _items[purchaseReturn.Id] = items.ToList();
        return Task.CompletedTask;
    }

    public Task UpdateSettlementAsync(Guid id, OrderSettlementStatus settlement, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        _calls?.Add("UpdateSettlement");
        var purchaseReturn = _returns[id];
        purchaseReturn.SettlementStatus = settlement;
        purchaseReturn.UpdatedBy = operatorId;
        purchaseReturn.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(Guid id, OrderStatus status, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        _calls?.Add("UpdateStatus");
        var purchaseReturn = _returns[id];
        purchaseReturn.Status = status;
        purchaseReturn.UpdatedBy = operatorId;
        purchaseReturn.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<string> GenerateReturnNoAsync(string prefix, DateTimeOffset returnDate, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Generate");
        var pattern = $"{prefix}{returnDate.UtcDateTime:yyyyMMdd}";
        // 与真实仓储一致：序号 = 已存单据中前缀（前缀 + yyyyMMdd）匹配数 + 1
        var seq = _returns.Values.Count(r => r.ReturnNo.StartsWith(pattern)) + 1;
        return Task.FromResult($"{pattern}{seq:D4}");
    }
}
