using App.Core.Entities;
using App.Core.Features.Reports;

namespace App.Core.Abstractions;

/// <summary>
/// 报表跨表只读查询仓储接口（实现见 App.Infrastructure）。
/// 跨多张业务表的只读聚合不属于任何单一单据仓储，故按「查询职责」独立成接口（同 ISettlementQueryRepository 模式）；
/// 本规格纯只读：不新增写路径，消费既有的 StockMovements / Inventory / Products / Categories 与四张单据表。
/// 口径正文见 specs/025-erp-report/design.md §0.1。
/// </summary>
public interface IReportQueryRepository
{
    /// <summary>
    /// 进销存报表：期初（<paramref name="start"/> 之前的全部流水累计）+ 区间入 / 出（<paramref name="start"/> &lt;= t &lt; <paramref name="end"/>，
    /// 盘点调整按符号双向拆分）两段查询按商品合并，期末 = 期初 + 入 − 出；只含启用商品，按商品编码升序。
    /// 合计为**全量筛选结果**口径（不分页）；<paramref name="onlyChanged"/> 为真时只返回期间有变动的商品。
    /// </summary>
    /// <param name="start">期间起（含，UTC）</param>
    /// <param name="end">期间止（不含，UTC）</param>
    /// <param name="productId">商品 id，可空</param>
    /// <param name="categoryId">分类 id，可空</param>
    /// <param name="onlyChanged">是否只看期间有变动的商品</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<InventoryFlowItem> Items, int Total, InventoryFlowTotal Summary)> GetInventoryFlowAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? productId,
        Guid? categoryId,
        bool onlyChanged,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 库存余额表：启用商品按分类聚合（商品数 / 库存合计 / 零库存数 / 低库存数），keyword 模糊匹配商品编码与名称，
    /// 按分类名称升序；合计为**全量筛选结果**口径（不分页）。
    /// </summary>
    /// <param name="keyword">商品编码 / 名称关键词，可空</param>
    /// <param name="categoryId">分类 id，可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<StockBalanceItem> Items, int Total, StockBalanceTotal Summary)> GetStockBalanceAsync(
        string? keyword,
        Guid? categoryId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 采购汇总：期间内未作废的采购入库单与采购退货单按分组维度聚合（入库单数 / 入库数量与金额 / 退货数量与金额），
    /// 退货侧与入库侧按分组键取并集（无退货的分组退货列为 0，无入库的分组入库列为 0），金额取明细小计合计。
    /// 按分组名称升序；合计为**全量筛选结果**口径（不分页）。
    /// </summary>
    /// <param name="start">期间起（含，UTC）</param>
    /// <param name="end">期间止（不含，UTC）</param>
    /// <param name="partnerId">供应商 id，可空</param>
    /// <param name="groupByProduct">是否按商品分组（false 表示按往来单位分组）</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<PurchaseSummaryItem> Items, int Total, PurchaseSummaryTotal Summary)> GetPurchaseSummaryAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? partnerId,
        bool groupByProduct,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 销售汇总：口径同采购汇总，替换为销售出库单与销售退货单（往来维度为客户）。
    /// </summary>
    /// <param name="start">期间起（含，UTC）</param>
    /// <param name="end">期间止（不含，UTC）</param>
    /// <param name="partnerId">客户 id，可空</param>
    /// <param name="groupByProduct">是否按商品分组（false 表示按往来单位分组）</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<SalesSummaryItem> Items, int Total, SalesSummaryTotal Summary)> GetSalesSummaryAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? partnerId,
        bool groupByProduct,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 成本与毛利报表（erp-cost）：期间内销售出库 / 销售退货，按单据 / 商品 / 往来单位维度聚合
    /// 「销售收入（单据）+ 销售成本（流水 <c>TotalCost</c>，退货天然冲减）+ 成本缺失标记」；
    /// 毛利与毛利率为派生值，由用例计算。合计为**全量筛选结果**口径（不分页）。
    /// </summary>
    /// <param name="start">期间起（含，UTC）</param>
    /// <param name="end">期间止（不含，UTC）</param>
    /// <param name="productId">商品 id，可空</param>
    /// <param name="categoryId">分类 id，可空</param>
    /// <param name="groupBy">分组维度</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<CostProfitItem> Items, int Total, CostProfitTotal Summary)> GetCostProfitAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? productId,
        Guid? categoryId,
        CostProfitGroupBy groupBy,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
