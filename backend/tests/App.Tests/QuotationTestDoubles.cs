using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Tests;

/// <summary>
/// 行为型报价单仓储假实现（内存存储 + 记录调用）。
/// 规避 InMemory 提供程序不支持 <c>ExecuteUpdateAsync</c> 的关系型限制（design.md §6）。
/// 可选注入一个共享 <see cref="List{T}"/> 记录跨仓储的调用序列（断言转单同一事务内的调用顺序）。
/// </summary>
internal sealed class FakeQuotationRepository : IQuotationRepository
{
    private readonly Dictionary<Guid, Quotation> _quotations = [];
    private readonly Dictionary<Guid, List<QuotationItem>> _items = [];
    private readonly List<string>? _calls;

    public FakeQuotationRepository(List<string>? calls = null) => _calls = calls;

    /// <summary>分页查询注入结果（未设置时返回空页）</summary>
    public (IReadOnlyList<QuotationListItem> Items, int Total)? PagedResult { get; set; }

    /// <summary>最近一次分页查询入参，用于断言筛选透传</summary>
    public (string? Keyword, QuotationStatus? Status, DateTimeOffset? Start, DateTimeOffset? End, int Page, int PageSize)? LastPagedArgs { get; set; }

    /// <summary>已执行的单号前缀序列（断言报价单用 QT）</summary>
    public List<string> GeneratedPrefixes { get; } = [];

    /// <summary>已执行的状态回写序列（状态, 订单 id, 订单号），用于断言作废 / 转单回写</summary>
    public List<(QuotationStatus Status, Guid? OrderId, string? OrderNo)> StatusUpdates { get; } = [];

    /// <summary>已存报价单（供断言）</summary>
    public IReadOnlyCollection<Quotation> Quotations => _quotations.Values;

    /// <summary>预置一张报价单（供编辑 / 作废 / 转单 / 详情用例）</summary>
    public void Seed(Quotation quotation, IReadOnlyList<QuotationItem> items)
    {
        _quotations[quotation.Id] = quotation;
        _items[quotation.Id] = items.ToList();
    }

    /// <summary>读取报价单当前明细（供断言全量替换结果）</summary>
    public IReadOnlyList<QuotationItem> ItemsOf(Guid quotationId)
        => _items.TryGetValue(quotationId, out var list) ? list : Array.Empty<QuotationItem>();

    public Task<(IReadOnlyList<QuotationListItem> Items, int Total)> GetPagedAsync(
        string? keyword, QuotationStatus? status, DateTimeOffset? start, DateTimeOffset? end,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        _calls?.Add("GetPaged");
        LastPagedArgs = (keyword, status, start, end, page, pageSize);
        if (PagedResult is not null)
        {
            return Task.FromResult(PagedResult.Value);
        }

        IReadOnlyList<QuotationListItem> empty = Array.Empty<QuotationListItem>();
        return Task.FromResult((empty, 0));
    }

    public Task<(Quotation? Quotation, IReadOnlyList<QuotationItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_quotations.TryGetValue(id, out var quotation))
        {
            return Task.FromResult<(Quotation?, IReadOnlyList<QuotationItem>)>((null, Array.Empty<QuotationItem>()));
        }

        // 与真实仓储一致：按明细 Id 还原插入顺序
        IReadOnlyList<QuotationItem> items = _items[id].OrderBy(i => i.Id).ToList();
        return Task.FromResult((Quotation: (Quotation?)quotation, Items: items));
    }

    public Task AddAsync(Quotation quotation, IReadOnlyList<QuotationItem> items, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Add");
        _quotations[quotation.Id] = quotation;
        _items[quotation.Id] = items.ToList();
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Quotation quotation, IReadOnlyList<QuotationItem> items, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Update");
        _quotations[quotation.Id] = quotation;
        _items[quotation.Id] = items.ToList(); // 明细全量替换
        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(
        Guid id, QuotationStatus status, Guid? convertedOrderId, string? convertedOrderNo,
        Guid? operatorId, CancellationToken cancellationToken = default)
    {
        _calls?.Add("UpdateStatus");
        StatusUpdates.Add((status, convertedOrderId, convertedOrderNo));
        var quotation = _quotations[id];
        quotation.Status = status;
        quotation.ConvertedOrderId = convertedOrderId;
        quotation.ConvertedOrderNo = convertedOrderNo;
        quotation.UpdatedBy = operatorId;
        quotation.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<string> GenerateNoAsync(string prefix, DateTimeOffset quotationDate, CancellationToken cancellationToken = default)
    {
        _calls?.Add("Generate");
        GeneratedPrefixes.Add(prefix);
        var pattern = $"{prefix}{quotationDate.UtcDateTime:yyyyMMdd}";
        // 与真实仓储一致：序号 = 已存报价单中前缀（前缀 + yyyyMMdd）匹配数 + 1
        var seq = _quotations.Values.Count(q => q.QuotationNo.StartsWith(pattern)) + 1;
        return Task.FromResult($"{pattern}{seq:D4}");
    }
}
