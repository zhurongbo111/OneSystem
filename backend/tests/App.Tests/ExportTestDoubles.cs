using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Tests;

/// <summary>
/// 商品仓储的记录型假实现（erp-export 导出用例用）：记录列表查询入参、返回预设读模型，
/// 并按 id 字典提供商品编码批量查询（GetCodesByIdsAsync）。
/// 本用例不触及的成员抛 <see cref="NotSupportedException"/>（导出只读，不写）。
/// </summary>
internal sealed class RecordingProductRepository : IProductRepository
{
    /// <summary>预置列表查询结果</summary>
    public IReadOnlyList<ProductListItem> Items { get; set; } = [];

    /// <summary>预置商品编码（GetCodesByIdsAsync 用；缺失 id 不出现在结果中）</summary>
    public Dictionary<Guid, string> Codes { get; } = new();

    /// <summary>列表查询入参快照（keyword, categoryId, status, page, pageSize）</summary>
    public (string? Keyword, Guid? CategoryId, ProductStatus? Status, int Page, int PageSize)? LastPagedArgs { get; private set; }

    /// <summary>列表查询调用次数</summary>
    public int PagedCallCount { get; private set; }

    /// <summary>编码批量查询入参快照</summary>
    public IReadOnlyCollection<Guid>? LastCodeIds { get; private set; }

    /// <inheritdoc />
    public Task<(IReadOnlyList<ProductListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? categoryId,
        ProductStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        PagedCallCount++;
        LastPagedArgs = (keyword, categoryId, status, page, pageSize);
        return Task.FromResult((Items, Items.Count));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<Guid, string>> GetCodesByIdsAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default)
    {
        LastCodeIds = productIds;
        return Task.FromResult<IReadOnlyDictionary<Guid, string>>(
            Codes.Where(kv => productIds.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value));
    }

    /// <inheritdoc />
    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
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
        => throw new NotSupportedException();
}

/// <summary>
/// 用户仓储的记录型假实现（erp-export 导出「创建人」列用）：按 id 字典返回显示名，并记录查询入参。
/// </summary>
internal sealed class RecordingUserRepository : IUserRepository
{
    /// <summary>预置显示名（id → 显示名；缺失 id 不出现在结果中，导出侧输出空串）</summary>
    public Dictionary<Guid, string> DisplayNames { get; } = new();

    /// <summary>显示名批量查询入参快照</summary>
    public IReadOnlyCollection<Guid>? LastDisplayNameIds { get; private set; }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesByIdsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        LastDisplayNameIds = userIds;
        return Task.FromResult<IReadOnlyDictionary<Guid, string>>(
            DisplayNames.Where(kv => userIds.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value));
    }

    /// <inheritdoc />
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<bool> ExistsByEmailAsync(string email, Guid? excludeUserId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<bool> ExistsByPhoneAsync(string phone, Guid? excludeUserId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<(IReadOnlyList<User> Items, int Total)> GetPagedAsync(
        string? keyword, UserStatus? status, int page, int pageSize, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task AddAsync(User user, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task UpdateLastLoginAsync(Guid id, DateTimeOffset lastLoginAt, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

/// <summary>
/// 采购入库单仓储的记录型假实现（erp-export 单据导出用例用）：记录列表查询入参、
/// 返回预设单据与明细，并统计明细批量查询次数（断言「一次查询，不 N+1」）。
/// </summary>
internal sealed class RecordingPurchaseReceiptRepository : IPurchaseReceiptRepository
{
    /// <summary>预置单据</summary>
    public IReadOnlyList<PurchaseReceipt> Orders { get; set; } = [];

    /// <summary>预置明细（按单据 id 归属，批量查询时原样返回）</summary>
    public IReadOnlyList<PurchaseReceiptItem> Items { get; set; } = [];

    /// <summary>列表查询入参快照（page, pageSize）</summary>
    public (int Page, int PageSize)? LastPagedArgs { get; private set; }

    /// <summary>明细批量查询调用次数</summary>
    public int ItemsCallCount { get; private set; }

    /// <inheritdoc />
    public Task<(IReadOnlyList<(PurchaseReceipt Order, int TotalQuantity)> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? partnerId,
        Guid? orderId,
        DateTimeOffset? start,
        DateTimeOffset? end,
        SettlementState? settlementState,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        LastPagedArgs = (page, pageSize);
        // 与真实仓储同口径：每行附带明细数量合计
        IReadOnlyList<(PurchaseReceipt Order, int TotalQuantity)> rows = Orders
            .Select(o => (Order: o, TotalQuantity: Items.Where(i => i.ReceiptId == o.Id).Sum(i => i.Quantity)))
            .ToList();
        return Task.FromResult((rows, Orders.Count));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PurchaseReceiptItem>> GetItemsByOrderIdsAsync(
        IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken = default)
    {
        ItemsCallCount++;
        IReadOnlyList<PurchaseReceiptItem> items = Items
            .Where(i => orderIds.Contains(i.ReceiptId))
            .OrderBy(i => i.Id)
            .ToList();
        return Task.FromResult(items);
    }

    /// <inheritdoc />
    public Task<(PurchaseReceipt? Order, IReadOnlyList<PurchaseReceiptItem> Items)> GetDetailAsync(
        Guid id, bool includeItems = true, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task AddAsync(PurchaseReceipt order, IReadOnlyList<PurchaseReceiptItem> items, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task AddSettledAmountAsync(Guid id, decimal delta, Guid? operatorId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task UpdateStatusAsync(Guid id, OrderStatus status, Guid? operatorId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<string> GenerateOrderNoAsync(string prefix, DateTimeOffset orderDate, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
