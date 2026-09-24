using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Tests;

/// <summary>
/// 行为型调拨单仓储假实现（内存存储 + 记录调用与分页入参）。
/// 规避 InMemory 提供程序不支持 <c>ExecuteUpdateAsync</c> 的关系型限制（见 design.md §6）。
/// <see cref="PagedQueries"/> 记录分页筛选入参（供 GetTransfers 用例断言传参透传）。
/// </summary>
internal sealed class FakeTransferRepository : ITransferRepository
{
    private readonly Dictionary<Guid, Transfer> _transfers = [];
    private readonly Dictionary<Guid, List<TransferItem>> _items = [];
    private readonly List<string>? _calls;

    public FakeTransferRepository(List<string>? calls = null) => _calls = calls;

    /// <summary>新增写失败注入：返回非 null 异常时 AddAsync 抛出（模拟数据库写入失败）</summary>
    public Func<Exception?>? AddFailure { get; set; }

    /// <summary>已执行的分页查询入参（keyword / fromWarehouseId / toWarehouseId / start / end / page / pageSize）</summary>
    public List<(string? Keyword, Guid? FromWarehouseId, Guid? ToWarehouseId, DateTimeOffset? Start, DateTimeOffset? End, int Page, int PageSize)> PagedQueries { get; } = [];

    /// <summary>分页查询返回的行（由用例预置）</summary>
    public IReadOnlyList<Transfer> PagedItems { get; set; } = Array.Empty<Transfer>();

    /// <summary>分页查询返回的总数（由用例预置）</summary>
    public int PagedTotal { get; set; }

    /// <summary>预置一张调拨单（供 Void / GetById 用例）</summary>
    public void Seed(Transfer transfer, IReadOnlyList<TransferItem> items)
    {
        _transfers[transfer.Id] = transfer;
        _items[transfer.Id] = items.ToList();
    }

    public Task<(IReadOnlyList<Transfer> Items, int Total)> GetPagedAsync(
        string? keyword, Guid? fromWarehouseId, Guid? toWarehouseId,
        DateTimeOffset? start, DateTimeOffset? end,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        PagedQueries.Add((keyword, fromWarehouseId, toWarehouseId, start, end, page, pageSize));
        return Task.FromResult((PagedItems, PagedTotal));
    }

    public Task<(Transfer? Transfer, IReadOnlyList<TransferItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_transfers.TryGetValue(id, out var transfer))
        {
            return Task.FromResult<(Transfer?, IReadOnlyList<TransferItem>)>((null, Array.Empty<TransferItem>()));
        }

        IReadOnlyList<TransferItem> items = _items[id].OrderBy(i => i.Id).ToList();
        return Task.FromResult((Transfer: (Transfer?)transfer, Items: items));
    }

    public Task AddAsync(Transfer transfer, IReadOnlyList<TransferItem> items, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Add");
        var failure = AddFailure?.Invoke();
        if (failure is not null)
        {
            throw failure;
        }

        _transfers[transfer.Id] = transfer;
        _items[transfer.Id] = items.ToList();
        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(Guid id, OrderStatus status, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        _calls?.Add("UpdateStatus");
        var transfer = _transfers[id];
        transfer.Status = status;
        transfer.UpdatedBy = operatorId;
        transfer.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<string> GenerateTransferNoAsync(DateTimeOffset transferDate, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Generate");
        const string prefix = "TR";
        var pattern = $"{prefix}{transferDate.UtcDateTime:yyyyMMdd}";
        // 与真实仓储一致：序号 = 已存单据中前缀匹配数 + 1
        var seq = _transfers.Values.Count(t => t.TransferNo.StartsWith(pattern)) + 1;
        return Task.FromResult($"{pattern}{seq:D4}");
    }
}
