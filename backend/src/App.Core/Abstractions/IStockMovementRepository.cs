using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 库存变动流水仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// 纯追加表：只追加与查询，无更新 / 删除；跨仓储原子性由调用方 IUnitOfWork 事务提供。
/// </summary>
public interface IStockMovementRepository
{
    /// <summary>
    /// 追加一条流水（单一仓储写由自身 SaveChangesAsync 保证；
    /// 跨仓储的原子性由调用方 IUnitOfWork 提供，写入点须位于既有事务块内）
    /// </summary>
    /// <param name="movement">流水实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AppendAsync(StockMovement movement, CancellationToken cancellationToken = default);

    /// <summary>
    /// 流水分页查询：联查 Products（Code / Name / Unit）与 Warehouses（Name）与 Users（DisplayName 作为操作人姓名）；
    /// keyword 模糊匹配 SourceNo；productId / type / warehouseId 精确匹配；start / end 对 CreatedAt 闭区间；
    /// 按 CreatedAt 倒序。
    /// </summary>
    /// <param name="keyword">来源单号关键词，可空</param>
    /// <param name="productId">商品 id，可空</param>
    /// <param name="warehouseId">仓库 id，可空（不传 = 全部仓，038）</param>
    /// <param name="type">变动类型，可空</param>
    /// <param name="start">变动时间起（UTC），可空</param>
    /// <param name="end">变动时间止（UTC），可空</param>
    /// <param name="page">页码（从 1 起）</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<StockMovementItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? productId,
        Guid? warehouseId,
        StockMovementType? type,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 流水变动量合计（对账 / 一致性校验用）：
    /// 指定仓时满足「同一 (商品, 仓) Σ Quantity == 该仓库存」，不指定仓时为组织级合计（Σ 各仓）。
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="warehouseId">仓库 id，可空（不传 = 全部仓合计）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<int> SumQuantityAsync(
        Guid productId, Guid? warehouseId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量查询「已发生过库存变动」的商品 id（期初建账限制用，erp-stock-take）：
    /// 在给定商品集合中，返回存在任意流水记录的商品 id 集合（空集合表示全部未发生变动）。
    /// 期初建账按仓判断（038）：传 warehouseId 时只看该仓的流水。
    /// </summary>
    /// <param name="productIds">商品 id 集合</param>
    /// <param name="warehouseId">仓库 id，可空（不传 = 全部仓）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已有流水变动的商品 id 集合</returns>
    Task<IReadOnlyCollection<Guid>> GetProductIdsWithMovementsAsync(
        IReadOnlyList<Guid> productIds, Guid? warehouseId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取某来源单据 + 商品的指定类型流水的成本单价（erp-cost；冲销类还原成本用）。
    /// 作废 / 退货作废一律复用**原方向**流水的 <c>UnitCost</c>，保证「入 + 冲回 = 0」；
    /// 无匹配流水（如历史数据、被退销售单无原单关联）时返回 <c>null</c>，由调用方按 §0.2 兜底。
    /// </summary>
    /// <param name="sourceId">来源单据 id</param>
    /// <param name="productId">商品 id</param>
    /// <param name="type">被还原的变动类型（如 <see cref="StockMovementType.PurchaseInbound"/>）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<decimal?> GetMovementUnitCostAsync(
        Guid sourceId,
        Guid productId,
        StockMovementType type,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 成本重算取数（erp-cost）：按 <c>CreatedAt, Id</c> 升序返回流水行（含 <c>WarehouseId</c>，038 后
    /// 重算按「商品 × 仓」分账），并左连接带出关联单价
    ///（采购入库取采购单明细单价、期初建账取盘点明细成本单价），避免重算时 N+1 反查。
    /// </summary>
    /// <param name="productId">商品 id，可空（不传表示全部商品）</param>
    /// <param name="start">期间起（含），可空</param>
    /// <param name="end">期间止（不含），可空</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<StockMovementCostRow>> GetAllForCostAsync(
        Guid? productId,
        DateTimeOffset? start,
        DateTimeOffset? end,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 写回单条流水的成本列（erp-cost）：**仅成本重算使用**（历史成本补齐 / 异常修复）。
    /// 流水既定约束为「只追加、不更新」，重算是唯一例外，且只改成本两列、不动数量与其它字段。
    /// </summary>
    /// <param name="id">流水 id</param>
    /// <param name="unitCost">成本单价</param>
    /// <param name="totalCost">成本金额（与 Quantity 同号）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateCostAsync(
        Guid id,
        decimal unitCost,
        decimal totalCost,
        CancellationToken cancellationToken = default);
}
