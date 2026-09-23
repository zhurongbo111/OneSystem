using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Tests;

/// <summary>
/// 测试用仓库常量（038）：默认仓 id 固定，便于假实现与用例断言共用同一取值。
/// </summary>
internal static class TestWarehouse
{
    /// <summary>默认仓 id（假实现的兜底仓；与迁移内置默认仓无关，仅测试内自洽）</summary>
    public static readonly Guid DefaultId = Guid.Parse("00000000-0000-0000-0000-0000000000aa");

    /// <summary>默认仓名称</summary>
    public const string DefaultName = "默认仓";
}

/// <summary>
/// 行为型仓库仓储假实现（内存存储 + 记录调用）：
/// 构造时预置一个启用仓（<see cref="TestWarehouse.DefaultId"/>，标记为默认仓），
/// 使「不传 warehouseId → 默认仓」的既有用例零改造继续成立。
/// </summary>
internal sealed class FakeWarehouseRepository : IWarehouseRepository
{
    private readonly Dictionary<Guid, Warehouse> _warehouses = [];

    /// <summary>已执行的默认仓查询次数（供断言兜底路径被走到）</summary>
    public int DefaultQueries { get; private set; }

    /// <summary>已执行的启用仓查询次数</summary>
    public int EnabledQueries { get; private set; }

    /// <summary>
    /// 初始化假仓储并预置默认仓
    /// </summary>
    public FakeWarehouseRepository()
    {
        var now = DateTimeOffset.UtcNow;
        _warehouses[TestWarehouse.DefaultId] = new Warehouse
        {
            Id = TestWarehouse.DefaultId,
            Code = "DEFAULT",
            Name = TestWarehouse.DefaultName,
            IsDefault = true,
            Status = PartnerStatus.Enabled,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>预置仓库（覆盖同 id 已有记录）</summary>
    public void Seed(Warehouse warehouse) => _warehouses[warehouse.Id] = warehouse;

    /// <summary>新增一个启用仓（供多仓用例）</summary>
    public Warehouse AddEnabled(Guid id, string code, string name)
    {
        var now = DateTimeOffset.UtcNow;
        var warehouse = new Warehouse
        {
            Id = id,
            Code = code,
            Name = name,
            IsDefault = false,
            Status = PartnerStatus.Enabled,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _warehouses[id] = warehouse;
        return warehouse;
    }

    /// <summary>已有仓库（供断言）</summary>
    public IReadOnlyCollection<Warehouse> Warehouses => _warehouses.Values;

    /// <inheritdoc />
    public Task<Warehouse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_warehouses.TryGetValue(id, out var warehouse) ? warehouse : null);

    /// <inheritdoc />
    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var lower = code.Trim().ToLowerInvariant();
        return Task.FromResult(_warehouses.Values.Any(w =>
            w.Code.ToLowerInvariant() == lower && (excludeId is null || w.Id != excludeId.Value)));
    }

    /// <inheritdoc />
    public Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var lower = name.Trim().ToLowerInvariant();
        return Task.FromResult(_warehouses.Values.Any(w =>
            w.Name.ToLowerInvariant() == lower && (excludeId is null || w.Id != excludeId.Value)));
    }

    /// <inheritdoc />
    public Task<(IReadOnlyList<Warehouse> Items, int Total)> GetPagedAsync(
        string? keyword,
        PartnerStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _warehouses.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(w =>
                w.Code.ToLowerInvariant().Contains(lower) || w.Name.ToLowerInvariant().Contains(lower));
        }

        if (status is not null)
        {
            query = query.Where(w => w.Status == status.Value);
        }

        var ordered = query.OrderByDescending(w => w.IsDefault).ThenBy(w => w.Code).ToList();
        IReadOnlyList<Warehouse> items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult((items, ordered.Count));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Warehouse>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        EnabledQueries++;
        IReadOnlyList<Warehouse> items = _warehouses.Values
            .Where(w => w.Status == PartnerStatus.Enabled)
            .OrderBy(w => w.Code)
            .ToList();
        return Task.FromResult(items);
    }

    /// <inheritdoc />
    public Task<Warehouse?> GetDefaultAsync(CancellationToken cancellationToken = default)
    {
        DefaultQueries++;
        return Task.FromResult(_warehouses.Values.FirstOrDefault(w => w.IsDefault));
    }

    /// <inheritdoc />
    public Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken = default)
    {
        _warehouses[warehouse.Id] = warehouse;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(Warehouse warehouse, CancellationToken cancellationToken = default)
    {
        _warehouses[warehouse.Id] = warehouse;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearDefaultAsync(Guid? exceptId, CancellationToken cancellationToken = default)
    {
        foreach (var warehouse in _warehouses.Values)
        {
            if (exceptId is null || warehouse.Id != exceptId.Value)
            {
                warehouse.IsDefault = false;
            }
        }

        return Task.CompletedTask;
    }
}
