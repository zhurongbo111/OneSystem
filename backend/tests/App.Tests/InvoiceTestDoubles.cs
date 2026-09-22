using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Tests;

/// <summary>
/// 行为型发票仓储假实现（内存存储 + 记录分页 / 批量取明细入参），供导出用例断言筛选透传与工作表归属；
/// 登记 / 作废 / 详情用例使用真实仓储 + InMemory（写路径为普通 SaveChanges，不依赖关系型特性）。
/// </summary>
internal sealed class FakeInvoiceRepository : IInvoiceRepository
{
    private readonly Dictionary<Guid, Invoice> _invoices = [];
    private readonly Dictionary<Guid, List<InvoiceItem>> _items = [];

    /// <summary>已执行的分页查询入参</summary>
    public List<(string? Keyword, InvoiceType? Type, Guid? PartnerId, DateTimeOffset? Start, DateTimeOffset? End, int Page, int PageSize)> PagedQueries
    { get; } = [];

    /// <summary>分页查询返回的行（由用例预置）</summary>
    public IReadOnlyList<InvoiceListItem> PagedItems { get; set; } = Array.Empty<InvoiceListItem>();

    /// <summary>分页查询返回的总数（由用例预置）</summary>
    public int PagedTotal { get; set; }

    /// <summary>批量取明细的调用次数 / 入参记录</summary>
    public List<int> ItemBatchSizes { get; } = [];

    /// <summary>预置一张发票（供详情 / 作废 / 导出用例）</summary>
    public void Seed(Invoice invoice, IReadOnlyList<InvoiceItem> items)
    {
        _invoices[invoice.Id] = invoice;
        _items[invoice.Id] = items.ToList();
    }

    /// <summary>读取发票当前状态（供断言）</summary>
    public Invoice Get(Guid id) => _invoices[id];

    public Task<(IReadOnlyList<InvoiceListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        InvoiceType? type,
        Guid? partnerId,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        PagedQueries.Add((keyword, type, partnerId, start, end, page, pageSize));
        return Task.FromResult((PagedItems, PagedTotal));
    }

    public Task<(Invoice? Invoice, IReadOnlyList<InvoiceItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_invoices.TryGetValue(id, out var invoice))
        {
            return Task.FromResult<(Invoice?, IReadOnlyList<InvoiceItem>)>((null, Array.Empty<InvoiceItem>()));
        }

        // 与真实仓储一致：按明细 Id 还原插入顺序
        IReadOnlyList<InvoiceItem> items = _items[id].OrderBy(i => i.Id).ToList();
        return Task.FromResult((Invoice: (Invoice?)invoice, Items: items));
    }

    public Task<bool> ExistsByInvoiceNoAsync(string invoiceNo, CancellationToken cancellationToken = default)
        => Task.FromResult(_invoices.Values.Any(v => string.Equals(v.InvoiceNo, invoiceNo, StringComparison.OrdinalIgnoreCase)));

    public Task AddAsync(Invoice invoice, IReadOnlyList<InvoiceItem> items, CancellationToken cancellationToken = default)
    {
        _invoices[invoice.Id] = invoice;
        _items[invoice.Id] = items.ToList();
        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(Guid id, OrderStatus status, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        var invoice = _invoices[id];
        invoice.Status = status;
        invoice.UpdatedBy = operatorId;
        invoice.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<InvoiceItem>> GetItemsByInvoiceIdsAsync(IReadOnlyCollection<Guid> invoiceIds, CancellationToken cancellationToken = default)
    {
        ItemBatchSizes.Add(invoiceIds.Count);
        IReadOnlyList<InvoiceItem> items = _items
            .Where(kv => invoiceIds.Contains(kv.Key))
            .SelectMany(kv => kv.Value)
            .OrderBy(i => i.Id)
            .ToList();
        return Task.FromResult(items);
    }
}