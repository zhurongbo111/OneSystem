using App.Core.Abstractions;

namespace App.Tests;

/// <summary>进销存报表查询的传参快照（供用例侧断言筛选与分页原样透传）</summary>
internal readonly record struct InventoryFlowArgs(
    DateTimeOffset Start,
    DateTimeOffset End,
    Guid? ProductId,
    Guid? CategoryId,
    bool OnlyChanged,
    int Page,
    int PageSize);

/// <summary>库存余额表查询的传参快照</summary>
internal readonly record struct StockBalanceArgs(
    string? Keyword,
    Guid? CategoryId,
    int Page,
    int PageSize);

/// <summary>汇总报表查询的传参快照（采购 / 销售共用）</summary>
internal readonly record struct SummaryArgs(
    DateTimeOffset Start,
    DateTimeOffset End,
    Guid? PartnerId,
    bool GroupByProduct,
    int Page,
    int PageSize);

/// <summary>
/// 报表只读查询仓储行为型假实现（不查库）：记录最后一次调用入参，返回用例预设的结果。
/// 用于验证 Handler 的传参透传、出参映射与合计计算；口径与 SQL 语义由
/// <see cref="ReportQueryRepositoryTests"/> 以真实仓储验证。
/// </summary>
internal sealed class FakeReportQueryRepository : IReportQueryRepository
{
    /// <summary>进销存报表：上一次调用入参</summary>
    public InventoryFlowArgs? LastInventoryFlowArgs { get; private set; }

    /// <summary>进销存报表：预设行</summary>
    public IReadOnlyList<InventoryFlowItem> InventoryFlowItems { get; set; } = [];

    /// <summary>进销存报表：预设总条数</summary>
    public int InventoryFlowTotal { get; set; }

    /// <summary>进销存报表：预设合计</summary>
    public InventoryFlowTotal InventoryFlowSummary { get; set; } = new()
    {
        OpeningQuantity = 0,
        InboundQuantity = 0,
        OutboundQuantity = 0,
        ClosingQuantity = 0,
    };

    /// <summary>库存余额表：上一次调用入参</summary>
    public StockBalanceArgs? LastStockBalanceArgs { get; private set; }

    /// <summary>库存余额表：预设行</summary>
    public IReadOnlyList<StockBalanceItem> StockBalanceItems { get; set; } = [];

    /// <summary>库存余额表：预设总条数</summary>
    public int StockBalanceTotal { get; set; }

    /// <summary>库存余额表：预设合计</summary>
    public StockBalanceTotal StockBalanceSummary { get; set; } = new()
    {
        ProductCount = 0,
        TotalQuantity = 0,
        ZeroStockCount = 0,
        BelowSafetyCount = 0,
    };

    /// <summary>采购汇总：上一次调用入参</summary>
    public SummaryArgs? LastPurchaseSummaryArgs { get; private set; }

    /// <summary>采购汇总：预设行</summary>
    public IReadOnlyList<PurchaseSummaryItem> PurchaseSummaryItems { get; set; } = [];

    /// <summary>采购汇总：预设总条数</summary>
    public int PurchaseSummaryTotal { get; set; }

    /// <summary>采购汇总：预设合计</summary>
    public PurchaseSummaryTotal PurchaseSummarySummary { get; set; } = new()
    {
        OrderCount = 0,
        InboundQuantity = 0,
        InboundAmount = 0,
        ReturnQuantity = 0,
        ReturnAmount = 0,
    };

    /// <summary>销售汇总：上一次调用入参</summary>
    public SummaryArgs? LastSalesSummaryArgs { get; private set; }

    /// <summary>销售汇总：预设行</summary>
    public IReadOnlyList<SalesSummaryItem> SalesSummaryItems { get; set; } = [];

    /// <summary>销售汇总：预设总条数</summary>
    public int SalesSummaryTotal { get; set; }

    /// <summary>销售汇总：预设合计</summary>
    public SalesSummaryTotal SalesSummarySummary { get; set; } = new()
    {
        OrderCount = 0,
        OutboundQuantity = 0,
        OutboundAmount = 0,
        ReturnQuantity = 0,
        ReturnAmount = 0,
    };

    /// <inheritdoc />
    public Task<(IReadOnlyList<InventoryFlowItem> Items, int Total, InventoryFlowTotal Summary)> GetInventoryFlowAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? productId,
        Guid? categoryId,
        bool onlyChanged,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        LastInventoryFlowArgs = new InventoryFlowArgs(start, end, productId, categoryId, onlyChanged, page, pageSize);
        return Task.FromResult((InventoryFlowItems, InventoryFlowTotal, InventoryFlowSummary));
    }

    /// <inheritdoc />
    public Task<(IReadOnlyList<StockBalanceItem> Items, int Total, StockBalanceTotal Summary)> GetStockBalanceAsync(
        string? keyword,
        Guid? categoryId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        LastStockBalanceArgs = new StockBalanceArgs(keyword, categoryId, page, pageSize);
        return Task.FromResult((StockBalanceItems, StockBalanceTotal, StockBalanceSummary));
    }

    /// <inheritdoc />
    public Task<(IReadOnlyList<PurchaseSummaryItem> Items, int Total, PurchaseSummaryTotal Summary)> GetPurchaseSummaryAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? partnerId,
        bool groupByProduct,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        LastPurchaseSummaryArgs = new SummaryArgs(start, end, partnerId, groupByProduct, page, pageSize);
        return Task.FromResult((PurchaseSummaryItems, PurchaseSummaryTotal, PurchaseSummarySummary));
    }

    /// <inheritdoc />
    public Task<(IReadOnlyList<SalesSummaryItem> Items, int Total, SalesSummaryTotal Summary)> GetSalesSummaryAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? partnerId,
        bool groupByProduct,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        LastSalesSummaryArgs = new SummaryArgs(start, end, partnerId, groupByProduct, page, pageSize);
        return Task.FromResult((SalesSummaryItems, SalesSummaryTotal, SalesSummarySummary));
    }
}
