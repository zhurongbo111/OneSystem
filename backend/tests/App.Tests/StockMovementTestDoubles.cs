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

    /// <summary>预置的采购入库明细单价（sourceId, productId）→ 单价（成本重算取数用）</summary>
    public Dictionary<(Guid SourceId, Guid ProductId), decimal> PurchaseUnitPrices { get; } = new();

    /// <summary>预置的期初建账成本单价（sourceId, productId）→ 单价（成本重算取数用）</summary>
    public Dictionary<(Guid SourceId, Guid ProductId), decimal> InitialUnitCosts { get; } = new();

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

    public Task<decimal?> GetMovementUnitCostAsync(
        Guid sourceId, Guid productId, StockMovementType type, CancellationToken cancellationToken = default)
    {
        // 冲销还原成本：在已追加的流水中反查「原方向」单价（无匹配返回 null，由调用方兜底）
        var matched = Appended
            .Where(m => m.SourceId == sourceId && m.ProductId == productId && m.MovementType == type)
            .Select(m => (decimal?)m.UnitCost)
            .FirstOrDefault();
        return Task.FromResult(matched);
    }

    /// <summary>成本写回序列（id, unitCost, totalCost），供重算用例断言</summary>
    public List<(Guid Id, decimal UnitCost, decimal TotalCost)> CostUpdates { get; } = new();

    public Task UpdateCostAsync(
        Guid id, decimal unitCost, decimal totalCost, CancellationToken cancellationToken = default)
    {
        _calls?.Add("UpdateCost");
        CostUpdates.Add((id, unitCost, totalCost));

        // 同步回写已追加的流水，便于重算后直接断言流水成本列
        var movement = Appended.FirstOrDefault(m => m.Id == id);
        if (movement is not null)
        {
            movement.UnitCost = unitCost;
            movement.TotalCost = totalCost;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StockMovementCostRow>> GetAllForCostAsync(
        Guid? productId, DateTimeOffset? start, DateTimeOffset? end, CancellationToken cancellationToken = default)
    {
        var rows = Appended
            .Where(m => productId is null || m.ProductId == productId)
            .Where(m => start is null || m.CreatedAt >= start)
            .Where(m => end is null || m.CreatedAt < end)
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Select(m => new StockMovementCostRow
            {
                Id = m.Id,
                ProductId = m.ProductId,
                MovementType = m.MovementType,
                Quantity = m.Quantity,
                SourceId = m.SourceId,
                SourceNo = m.SourceNo,
                CreatedAt = m.CreatedAt,
                PurchaseUnitPrice = m.SourceId is not null
                    && PurchaseUnitPrices.TryGetValue((m.SourceId.Value, m.ProductId), out var price)
                    ? price
                    : null,
                InitialUnitCost = m.SourceId is not null
                    && InitialUnitCosts.TryGetValue((m.SourceId.Value, m.ProductId), out var cost)
                    ? cost
                    : null,
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<StockMovementCostRow>>(rows);
    }
}
