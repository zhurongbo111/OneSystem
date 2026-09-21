using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Tests;

/// <summary>
/// 盘点单假仓储（行为型内存实现）。
/// 单号生成与真实仓储同机制：ST + yyyyMMdd + COUNT(同前缀)+1 补零（见 design.md §2.1）；
/// AddAsync 支持单号冲突失败注入，供重试路径测试。
/// </summary>
internal sealed class FakeStockTakeRepository : IStockTakeRepository
{
    /// <summary>已保存的盘点单主表</summary>
    public List<StockTake> Takes { get; } = [];

    /// <summary>已保存的明细（按 StockTakeId 关联）</summary>
    public List<StockTakeItem> Items { get; } = [];

    /// <summary>单号生成调用次数（含重试）</summary>
    public int GenerateCount { get; private set; }

    /// <summary>AddAsync 失败注入：前 N 次调用抛单号冲突（模拟唯一索引并发冲突）</summary>
    public int AddConflictCount { get; set; }

    private readonly List<string>? _calls;
    private int _addCalls;

    public FakeStockTakeRepository(List<string>? calls = null) => _calls = calls;

    /// <inheritdoc />
    public Task AddAsync(StockTake take, IReadOnlyList<StockTakeItem> items, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Add");
        _addCalls++;
        if (_addCalls <= AddConflictCount)
        {
            throw new OrderNoConflictException(new Exception("23505 unique_violation"));
        }

        Takes.Add(take);
        Items.AddRange(items);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<(IReadOnlyList<StockTake> Items, int Total)> GetPagedAsync(
        string? keyword,
        StockTakeType? type,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = Takes.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(t => t.TakeNo.ToLower().Contains(lower));
        }

        if (type is not null)
        {
            var v = type.Value;
            query = query.Where(t => t.Type == v);
        }

        if (start is not null)
        {
            var s = start.Value;
            query = query.Where(t => t.TakeDate >= s);
        }

        if (end is not null)
        {
            var e = end.Value;
            query = query.Where(t => t.TakeDate <= e);
        }

        var ordered = query.OrderByDescending(t => t.CreatedAt).ToList();
        var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult((Items: (IReadOnlyList<StockTake>)pageItems, Total: ordered.Count));
    }

    /// <inheritdoc />
    public Task<(StockTake? Take, IReadOnlyList<StockTakeItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var take = Takes.FirstOrDefault(t => t.Id == id);
        if (take is null)
        {
            return Task.FromResult<(StockTake?, IReadOnlyList<StockTakeItem>)>((null, Array.Empty<StockTakeItem>()));
        }

        var takeItems = Items.Where(i => i.StockTakeId == id).OrderBy(i => i.Id).ToList();
        return Task.FromResult((Take: (StockTake?)take, Items: (IReadOnlyList<StockTakeItem>)takeItems));
    }

    /// <summary>批量取明细（erp-export 导出用）：与真实仓储同口径，按明细 Id 升序</summary>
    public Task<IReadOnlyList<StockTakeItem>> GetItemsByTakeIdsAsync(IReadOnlyCollection<Guid> takeIds, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<StockTakeItem> items = Items
            .Where(i => takeIds.Contains(i.StockTakeId))
            .OrderBy(i => i.Id)
            .ToList();
        return Task.FromResult(items);
    }

    /// <inheritdoc />
    public Task<string> GenerateTakeNoAsync(DateTimeOffset takeDate, CancellationToken cancellationToken = default)
    {
        GenerateCount++;
        var dateSegment = takeDate.UtcDateTime.ToString("yyyyMMdd");
        var pattern = $"ST{dateSegment}";
        var count = Takes.Count(t => t.TakeNo.StartsWith(pattern));
        return Task.FromResult($"{pattern}{(count + 1):D4}");
    }
}

/// <summary>
/// 商品假仓储（CreateStockTake 商品校验 / 快照 + GetStockTakePickProducts 用）。
/// </summary>
internal sealed class FakeProductRepository : IProductRepository
{
    /// <summary>按 id 提供的商品（不存在则返回 null）</summary>
    public Dictionary<Guid, Product> ById { get; } = [];

    /// <summary>开单选择读模型（启用商品 + 当前库存；停用商品不在此列，模拟仓储已过滤）</summary>
    public List<ProductPickItem> Picks { get; } = [];

    /// <inheritdoc />
    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(ById.TryGetValue(id, out var p) ? p : null);

    /// <inheritdoc />
    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(ById.Values.Any(p => p.Code == code && p.Id != excludeId));

    /// <inheritdoc />
    public Task<(IReadOnlyList<ProductListItem> Items, int Total)> GetPagedAsync(
        string? keyword, Guid? categoryId, ProductStatus? status, int page, int pageSize, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<ProductDetail?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task AddAsync(Product product, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<IReadOnlyList<ProductPickItem>> GetPickListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ProductPickItem>>(Picks.ToList());

    /// <summary>批量取商品编码（erp-export 导出用）：与真实仓储同口径，缺失 id 不出现在结果中</summary>
    public Task<IReadOnlyDictionary<Guid, string>> GetCodesByIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, string>>(
            ById.Where(kv => productIds.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value.Code));
}
