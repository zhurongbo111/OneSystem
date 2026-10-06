using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Batches;

namespace App.Tests;

/// <summary>
/// 明细行批次解析测试（040 §3.4，六类单据共用）：
/// 非批次商品传批次 → 40000；按批次商品缺批次 → 40127；批次不存在 / 不属于商品 → 40400；
/// 停用 → 40000；出库类过期 → 40128（入库类不拦）；就地新建（重复 40129 / 幂等复用 / 日期倒挂 40000）。
/// </summary>
public class BatchLineResolverTests
{
    /// <summary>固定「今天」（2026-09-30 UTC 午夜，与生产 <c>UtcNow.Date</c> 口径一致）</summary>
    private static DateTimeOffset Today { get; } = new(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);

    private static readonly Guid OperatorId = Guid.NewGuid();

    private static DateTimeOffset Day(int year, int month, int day) => new(year, month, day, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task 非批次商品_未传批次_应返回null()
    {
        var product = Guid.NewGuid();
        var result = await BatchLineResolver.ResolveAsync(false, product, null, null, null, null, new FakeBatchRepository(), false, Today, OperatorId);

        Assert.Null(result);
    }

    [Fact]
    public async Task 非批次商品_传批次_应报Validation()
    {
        var product = Guid.NewGuid();
        var repo = new FakeBatchRepository();
        repo.Seed(product, "B1");
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => BatchLineResolver.ResolveAsync(false, product, repo.IdOf(product, "B1"), null, null, null, repo, false, Today, OperatorId));

        Assert.Equal(ErrorCode.Validation, ex.Code);
    }

    [Fact]
    public async Task 按批次商品_缺批次_应报BatchRequired()
    {
        var product = Guid.NewGuid();
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => BatchLineResolver.ResolveAsync(true, product, null, null, null, null, new FakeBatchRepository(), false, Today, OperatorId));

        Assert.Equal(ErrorCode.BatchRequired, ex.Code);
    }

    [Fact]
    public async Task 批次id与新建号同传_应报Validation()
    {
        var product = Guid.NewGuid();
        var repo = new FakeBatchRepository();
        repo.Seed(product, "B1");
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => BatchLineResolver.ResolveAsync(true, product, repo.IdOf(product, "B1"), "B1", null, null, repo, false, Today, OperatorId));

        Assert.Equal(ErrorCode.Validation, ex.Code);
    }

    [Fact]
    public async Task 批次不存在_应报NotFound()
    {
        var product = Guid.NewGuid();
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => BatchLineResolver.ResolveAsync(true, product, Guid.NewGuid(), null, null, null, new FakeBatchRepository(), false, Today, OperatorId));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 批次不属于该商品_应报NotFound()
    {
        var otherProduct = Guid.NewGuid();
        var repo = new FakeBatchRepository();
        repo.Seed(otherProduct, "B1");
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => BatchLineResolver.ResolveAsync(true, Guid.NewGuid(), repo.IdOf(otherProduct, "B1"), null, null, null, repo, false, Today, OperatorId));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 停用批次_应报Validation()
    {
        var product = Guid.NewGuid();
        var repo = new FakeBatchRepository();
        repo.Seed(product, "B1", status: PartnerStatus.Disabled);
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => BatchLineResolver.ResolveAsync(true, product, repo.IdOf(product, "B1"), null, null, null, repo, false, Today, OperatorId));

        Assert.Equal(ErrorCode.Validation, ex.Code);
    }

    [Fact]
    public async Task 出库类_过期批次_应报BatchExpired()
    {
        var product = Guid.NewGuid();
        var repo = new FakeBatchRepository();
        repo.Seed(product, "B1", expiryDate: Day(2026, 9, 29)); // < 今天 09-30
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => BatchLineResolver.ResolveAsync(true, product, repo.IdOf(product, "B1"), null, null, null, repo, outbound: true, Today, OperatorId));

        Assert.Equal(ErrorCode.BatchExpired, ex.Code);
        Assert.Contains("B1", ex.Message);
    }

    [Fact]
    public async Task 入库类_过期批次_应放行()
    {
        var product = Guid.NewGuid();
        var repo = new FakeBatchRepository();
        repo.Seed(product, "B1", expiryDate: Day(2026, 9, 29));
        var result = await BatchLineResolver.ResolveAsync(true, product, repo.IdOf(product, "B1"), null, null, null, repo, outbound: false, Today, OperatorId);

        Assert.NotNull(result);
        Assert.Equal("B1", result!.BatchNo);
    }

    [Fact]
    public async Task 出库类_到期日当天_应放行()
    {
        var product = Guid.NewGuid();
        var repo = new FakeBatchRepository();
        repo.Seed(product, "B1", expiryDate: Day(2026, 9, 30)); // = 今天，未过期
        var result = await BatchLineResolver.ResolveAsync(true, product, repo.IdOf(product, "B1"), null, null, null, repo, outbound: true, Today, OperatorId);

        Assert.NotNull(result);
    }

    // ---------- 就地新建 ----------

    [Fact]
    public async Task 就地新建_应落库并返回()
    {
        var product = Guid.NewGuid();
        var repo = new FakeBatchRepository();
        var result = await BatchLineResolver.ResolveAsync(
            true, product, null, "NEW01", Day(2026, 1, 1), Day(2026, 10, 1), repo, outbound: false, Today, OperatorId);

        Assert.NotNull(result);
        Assert.Equal("NEW01", result!.BatchNo);
        var added = repo.Added.Single(b => b.BatchNo == "NEW01");
        Assert.Equal(added.Id, result.BatchId);
        Assert.Equal(OperatorId, added.CreatedBy);
    }

    [Fact]
    public async Task 就地新建_日期倒挂_应报Validation()
    {
        var product = Guid.NewGuid();
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => BatchLineResolver.ResolveAsync(
                true, product, null, "NEW01", Day(2026, 10, 1), Day(2026, 1, 1), new FakeBatchRepository(), false, Today, OperatorId));

        Assert.Equal(ErrorCode.Validation, ex.Code);
    }

    [Fact]
    public async Task 就地新建_批次号已存在_应报BatchNoExists()
    {
        var product = Guid.NewGuid();
        // 精确查无（幂等未命中）但存在性检查命中 → 触发 40129
        var repo = new ForceExistsBatchRepository();
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => BatchLineResolver.ResolveAsync(true, product, null, "DUP", null, null, repo, false, Today, OperatorId));

        Assert.Equal(ErrorCode.BatchNoExists, ex.Code);
    }

    [Fact]
    public async Task 就地新建_幂等复用既有批次()
    {
        var product = Guid.NewGuid();
        var repo = new FakeBatchRepository();
        repo.Seed(product, "B1", expiryDate: Day(2026, 10, 1));
        var before = repo.Added.Count;

        var result = await BatchLineResolver.ResolveAsync(true, product, null, "B1", null, null, repo, false, Today, OperatorId);

        Assert.NotNull(result);
        Assert.Equal("B1", result!.BatchNo);
        // 幂等：不重复落库
        Assert.Equal(before, repo.Added.Count);
    }
}

/// <summary>
/// 批次仓储替身：精确查（幂等）返回 null 但存在性检查恒真，用于触达就地新建的「重复 → 40129」分支
/// （真实 FakeBatchRepository 的两方法共享同一台账，无法区分幂等与重复）。
/// </summary>
internal sealed class ForceExistsBatchRepository : IBatchRepository
{
    public Task<Batch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult<Batch?>(null);

    public Task<Batch?> GetByProductAndBatchNoAsync(Guid productId, string batchNo, CancellationToken cancellationToken = default)
        => Task.FromResult<Batch?>(null);

    public Task<bool> ExistsByBatchNoAsync(Guid productId, string batchNo, Guid? excludeId, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<(IReadOnlyList<BatchListItem> Items, int Total)> GetPagedAsync(
        string? keyword, Guid? productId, PartnerStatus? status, bool? onlyExpiring,
        DateTimeOffset today, int page, int pageSize, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<IReadOnlyList<BatchPickItem>> GetPickListAsync(Guid productId, Guid warehouseId, DateTimeOffset today, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task AddAsync(Batch batch, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task UpdateAsync(Batch batch, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
