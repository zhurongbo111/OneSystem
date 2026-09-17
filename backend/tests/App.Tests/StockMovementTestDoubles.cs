using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Tests;

/// <summary>
/// 行为型库存流水仓储假实现：记录 AppendAsync 入参（供逐行参数断言），
/// 并保留流水以便对账（SumQuantityAsync = Σ 已记录流水变动量）。
/// </summary>
internal sealed class FakeStockMovementRepository : IStockMovementRepository
{
    private readonly List<string>? _calls;

    public FakeStockMovementRepository(List<string>? calls = null) => _calls = calls;

    /// <summary>已追加的流水（入参实体引用），用于类型 / 方向 / 来源 / 操作人 / 时间断言</summary>
    public List<StockMovement> Appended { get; } = new();

    public Task AppendAsync(StockMovement movement, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Append");
        Appended.Add(movement);
        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<StockMovementItem> Items, int Total)> GetPagedAsync(
        string? keyword, Guid? productId, StockMovementType? type,
        DateTimeOffset? start, DateTimeOffset? end,
        int page, int pageSize, CancellationToken cancellationToken = default)
        => Appended.Count == 0
            ? Task.FromResult<(IReadOnlyList<StockMovementItem>, int)>((Array.Empty<StockMovementItem>(), 0))
            : Task.FromException<(IReadOnlyList<StockMovementItem>, int)>(new NotSupportedException());

    public Task<int> SumQuantityAsync(Guid productId, CancellationToken cancellationToken = default)
        => Task.FromResult(Appended.Where(m => m.ProductId == productId).Sum(m => m.Quantity));

    public Task<IReadOnlyCollection<Guid>> GetProductIdsWithMovementsAsync(
        IReadOnlyList<Guid> productIds, CancellationToken cancellationToken = default)
    {
        var ids = Appended.Where(m => productIds.Contains(m.ProductId))
            .Select(m => m.ProductId)
            .Distinct()
            .ToList();
        return Task.FromResult<IReadOnlyCollection<Guid>>(ids);
    }
}
