using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Tests;

/// <summary>
/// 行为型客户协议价仓储假实现（内存字典 + 记录查询入参），供客户价用例断言筛选透传 / 出参映射 / 取价优先级。
/// 取价优先级（design.md §0.1）在假实现内复现：有协议价取协议价（`Agreement`），否则取商品销售价（`Default`）。
/// </summary>
internal sealed class FakePartnerPriceRepository : IPartnerPriceRepository
{
    private readonly Dictionary<Guid, PartnerPrice> _partnerPrices = [];

    /// <summary>批量取价的商品销售价兜底表（商品 id → 销售价）</summary>
    public Dictionary<Guid, decimal> ProductSalePrices { get; } = [];

    /// <summary>已执行的分页查询入参</summary>
    public List<(Guid? PartnerId, Guid? ProductId, string? Keyword, int Page, int PageSize)> PagedQueries { get; } = [];

    /// <summary>分页查询返回行（由用例预置）</summary>
    public IReadOnlyList<PartnerPriceListItem> PagedItems { get; set; } = Array.Empty<PartnerPriceListItem>();

    /// <summary>分页查询返回总数（由用例预置）</summary>
    public int PagedTotal { get; set; }

    /// <summary>已执行的批量取价入参（客户 id + 去重后的商品 id 集合）</summary>
    public List<(Guid PartnerId, IReadOnlyList<Guid> ProductIds)> EffectiveQueries { get; } = [];

    /// <summary>已执行的删除 id</summary>
    public List<Guid> DeletedIds { get; } = [];

    /// <summary>更新次数（协议价更新为跟踪实体写回，故只计次数、不重复入库）</summary>
    public int UpdateCount { get; private set; }

    /// <summary>预置一条协议价（供详情 / 编辑 / 删除 / 取价用例）</summary>
    public void Seed(PartnerPrice partnerPrice) => _partnerPrices[partnerPrice.Id] = partnerPrice;

    /// <summary>读取内存中当前的协议价（供断言写回结果）</summary>
    public PartnerPrice Get(Guid id) => _partnerPrices[id];

    /// <inheritdoc />
    public Task<PartnerPrice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_partnerPrices.TryGetValue(id, out var partnerPrice) ? partnerPrice : null);

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid partnerId, Guid productId, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(_partnerPrices.Values.Any(p => p.PartnerId == partnerId && p.ProductId == productId && p.Id != excludeId));

    /// <inheritdoc />
    public Task<(IReadOnlyList<PartnerPriceListItem> Items, int Total)> GetPagedAsync(
        Guid? partnerId,
        Guid? productId,
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        PagedQueries.Add((partnerId, productId, keyword, page, pageSize));
        return Task.FromResult((PagedItems, PagedTotal));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<EffectivePriceItem>> GetEffectiveAsync(
        Guid partnerId,
        IReadOnlyList<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        EffectiveQueries.Add((partnerId, productIds));

        IReadOnlyList<EffectivePriceItem> items = productIds.Select(productId =>
        {
            var agreement = _partnerPrices.Values.FirstOrDefault(p => p.PartnerId == partnerId && p.ProductId == productId);
            if (agreement is not null)
            {
                return new EffectivePriceItem
                {
                    ProductId = productId,
                    UnitPrice = agreement.Price,
                    Source = PriceSource.Agreement,
                };
            }

            if (ProductSalePrices.TryGetValue(productId, out var salePrice))
            {
                return new EffectivePriceItem
                {
                    ProductId = productId,
                    UnitPrice = salePrice,
                    Source = PriceSource.Default,
                };
            }

            throw new InvalidOperationException($"假实现缺少商品 {productId} 的销售价预置");
        }).ToList();

        return Task.FromResult(items);
    }

    /// <inheritdoc />
    public Task AddAsync(PartnerPrice partnerPrice, CancellationToken cancellationToken = default)
    {
        _partnerPrices[partnerPrice.Id] = partnerPrice;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(PartnerPrice partnerPrice, CancellationToken cancellationToken = default)
    {
        UpdateCount++;
        _partnerPrices[partnerPrice.Id] = partnerPrice;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        DeletedIds.Add(id);
        _partnerPrices.Remove(id);
        return Task.CompletedTask;
    }
}
