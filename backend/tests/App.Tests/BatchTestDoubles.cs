using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Tests;

/// <summary>
/// 测试时钟（040 批次过期 / 近效期判定注入固定「今天」用）
/// </summary>
internal sealed class TestClock(DateTimeOffset utcNow) : ISystemClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;

    public DateTimeOffset Today => new(UtcNow.UtcDateTime.Date, TimeSpan.Zero);
}

/// <summary>
/// 批次仓储假实现（040 单据批次解析 / 就地新建幂等 / 批次下拉）：
/// 内存台账 + 批次号唯一判定（大小写不敏感，与仓储实现同口径）。
/// </summary>
internal sealed class FakeBatchRepository : IBatchRepository
{
    private readonly Dictionary<Guid, Batch> _batches = [];

    /// <summary>预置批次档案</summary>
    public void Seed(
        Guid productId,
        string batchNo,
        Guid? id = null,
        PartnerStatus status = PartnerStatus.Enabled,
        DateTimeOffset? productionDate = null,
        DateTimeOffset? expiryDate = null)
    {
        var batch = new Batch
        {
            Id = id ?? Guid.NewGuid(),
            ProductId = productId,
            BatchNo = batchNo,
            Status = status,
            ProductionDate = productionDate,
            ExpiryDate = expiryDate,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _batches[batch.Id] = batch;
    }

    /// <summary>按批次号取预置 id（测试断言就地新建幂等复用时用）</summary>
    public Guid? IdOf(Guid productId, string batchNo)
        => _batches.Values
            .FirstOrDefault(b => b.ProductId == productId && string.Equals(b.BatchNo, batchNo, StringComparison.OrdinalIgnoreCase))
            ?.Id;

    /// <summary>已新增批次（就地新建断言用）</summary>
    public IReadOnlyList<Batch> Added => _batches.Values.ToList();

    public Task<Batch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult<Batch?>(_batches.GetValueOrDefault(id));

    public Task<Batch?> GetByProductAndBatchNoAsync(
        Guid productId, string batchNo, CancellationToken cancellationToken = default)
        => Task.FromResult<Batch?>(_batches.Values
            .FirstOrDefault(b => b.ProductId == productId && string.Equals(b.BatchNo, batchNo, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> ExistsByBatchNoAsync(
        Guid productId, string batchNo, Guid? excludeId, CancellationToken cancellationToken = default)
        => Task.FromResult(_batches.Values.Any(b =>
            b.ProductId == productId
            && b.Id != excludeId
            && string.Equals(b.BatchNo, batchNo, StringComparison.OrdinalIgnoreCase)));

    public Task<(IReadOnlyList<BatchListItem> Items, int Total)> GetPagedAsync(
        string? keyword, Guid? productId, PartnerStatus? status, bool? onlyExpiring,
        DateTimeOffset today, int page, int pageSize, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<IReadOnlyList<BatchPickItem>> GetPickListAsync(
        Guid productId, Guid warehouseId, DateTimeOffset today, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task AddAsync(Batch batch, CancellationToken cancellationToken = default)
    {
        _batches[batch.Id] = batch;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Batch batch, CancellationToken cancellationToken = default)
    {
        _batches[batch.Id] = batch;
        return Task.CompletedTask;
    }
}
