using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Tests;

/// <summary>
/// 行为型收付款单仓储假实现（内存存储 + 记录调用与分页入参）。
/// 规避 InMemory 提供程序不支持 <c>ExecuteUpdateAsync</c> 的关系型限制（见后端规则 §9）。
/// </summary>
internal sealed class FakeSettlementRepository : ISettlementRepository
{
    private readonly Dictionary<Guid, Settlement> _settlements = new();
    private readonly Dictionary<Guid, List<SettlementItem>> _items = new();
    private readonly List<string>? _calls;

    public FakeSettlementRepository(List<string>? calls = null) => _calls = calls;

    /// <summary>新增写失败注入：返回非 null 异常时 AddAsync 抛出（模拟数据库写入失败）</summary>
    public Func<Exception?>? AddFailure { get; set; }

    /// <summary>已执行的分页查询入参</summary>
    public List<(string? Keyword, SettlementType? Type, Guid? PartnerId, SettlementMethod? Method,
        DateTimeOffset? Start, DateTimeOffset? End, SettlementOrderType? OrderType, Guid? OrderId,
        int Page, int PageSize)> PagedQueries { get; } = new();

    /// <summary>分页查询返回的行（由用例预置）</summary>
    public IReadOnlyList<Settlement> PagedItems { get; set; } = Array.Empty<Settlement>();

    /// <summary>分页查询返回的总数（由用例预置）</summary>
    public int PagedTotal { get; set; }

    /// <summary>预置一张收付款单（供详情 / 作废用例）</summary>
    public void Seed(Settlement settlement, IReadOnlyList<SettlementItem> items)
    {
        _settlements[settlement.Id] = settlement;
        _items[settlement.Id] = items.ToList();
    }

    /// <summary>读取单据当前状态（供断言）</summary>
    public Settlement Get(Guid id) => _settlements[id];

    public Task<(IReadOnlyList<Settlement> Items, int Total)> GetPagedAsync(
        string? keyword, SettlementType? type, Guid? partnerId, SettlementMethod? method,
        DateTimeOffset? start, DateTimeOffset? end, SettlementOrderType? orderType, Guid? orderId,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        PagedQueries.Add((keyword, type, partnerId, method, start, end, orderType, orderId, page, pageSize));
        return Task.FromResult((PagedItems, PagedTotal));
    }

    public Task<(Settlement? Settlement, IReadOnlyList<SettlementItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_settlements.TryGetValue(id, out var settlement))
        {
            return Task.FromResult<(Settlement?, IReadOnlyList<SettlementItem>)>((null, Array.Empty<SettlementItem>()));
        }

        IReadOnlyList<SettlementItem> items = _items[id].OrderBy(i => i.Id).ToList(); // 与真实仓储一致：按 Id 还原插入顺序
        return Task.FromResult((Settlement: (Settlement?)settlement, Items: items));
    }

    /// <summary>批量取核销明细（列表「单据类型」列聚合与 erp-export 导出共用）：与真实仓储同口径，按明细 Id 升序</summary>
    public Task<IReadOnlyList<SettlementItem>> GetItemsBySettlementIdsAsync(IReadOnlyCollection<Guid> settlementIds, CancellationToken cancellationToken = default)
    {
        _calls?.Add("GetItemsBySettlementIds");
        IReadOnlyList<SettlementItem> items = _items
            .Where(kv => settlementIds.Contains(kv.Key))
            .SelectMany(kv => kv.Value)
            .OrderBy(i => i.Id)
            .ToList();
        return Task.FromResult(items);
    }

    public Task AddAsync(Settlement settlement, IReadOnlyList<SettlementItem> items, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Add");
        var failure = AddFailure?.Invoke();
        if (failure is not null)
        {
            throw failure;
        }

        _settlements[settlement.Id] = settlement;
        _items[settlement.Id] = items.ToList();
        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(Guid id, OrderStatus status, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        _calls?.Add("UpdateStatus");
        var settlement = _settlements[id];
        settlement.Status = status;
        settlement.UpdatedBy = operatorId;
        settlement.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<string> GenerateSettlementNoAsync(SettlementType type, DateTimeOffset settlementDate, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Generate");
        var prefix = type == SettlementType.Receipt ? "RC" : "PY";
        var pattern = $"{prefix}{settlementDate.UtcDateTime:yyyyMMdd}";
        // 与真实仓储一致：序号 = 已存单据中前缀（前缀 + yyyyMMdd）匹配数 + 1
        var seq = _settlements.Values.Count(s => s.SettlementNo.StartsWith(pattern)) + 1;
        return Task.FromResult($"{pattern}{seq:D4}");
    }
}

/// <summary>
/// 行为型结算跨表只读查询仓储假实现（记录入参 + 预置返回），供未结候选 / 往来台账用例断言透传与映射。
/// </summary>
internal sealed class FakeSettlementQueryRepository : ISettlementQueryRepository
{
    /// <summary>已执行的未结候选查询入参</summary>
    public List<(Guid PartnerId, SettlementType Type, int Page, int PageSize)> UnsettledQueries { get; } = new();

    /// <summary>未结候选返回行（由用例预置）</summary>
    public IReadOnlyList<SettlementCandidateItem> UnsettledItems { get; set; } = Array.Empty<SettlementCandidateItem>();

    /// <summary>未结候选返回总数（由用例预置）</summary>
    public int UnsettledTotal { get; set; }

    /// <summary>已执行的往来台账查询入参</summary>
    public List<(string? Keyword, PartnerType? Type, int Page, int PageSize)> ReconciliationQueries { get; } = new();

    /// <summary>往来台账返回行（由用例预置）</summary>
    public IReadOnlyList<ReconciliationItem> ReconciliationItems { get; set; } = Array.Empty<ReconciliationItem>();

    /// <summary>往来台账返回总数（由用例预置）</summary>
    public int ReconciliationTotal { get; set; }

    public Task<(IReadOnlyList<SettlementCandidateItem> Items, int Total)> GetUnsettledAsync(
        Guid partnerId, SettlementType type, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        UnsettledQueries.Add((partnerId, type, page, pageSize));
        return Task.FromResult((UnsettledItems, UnsettledTotal));
    }

    public Task<(IReadOnlyList<ReconciliationItem> Items, int Total)> GetReconciliationAsync(
        string? keyword, PartnerType? type, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        ReconciliationQueries.Add((keyword, type, page, pageSize));
        return Task.FromResult((ReconciliationItems, ReconciliationTotal));
    }
}
