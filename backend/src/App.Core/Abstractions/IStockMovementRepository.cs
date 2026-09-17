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
    /// 流水分页查询：联查 Products（Code / Name / Unit）与 Users（DisplayName 作为操作人姓名）；
    /// keyword 模糊匹配 SourceNo；productId / type 精确匹配；start / end 对 CreatedAt 闭区间；
    /// 按 CreatedAt 倒序。
    /// </summary>
    /// <param name="keyword">来源单号关键词，可空</param>
    /// <param name="productId">商品 id，可空</param>
    /// <param name="type">变动类型，可空</param>
    /// <param name="start">变动时间起（UTC），可空</param>
    /// <param name="end">变动时间止（UTC），可空</param>
    /// <param name="page">页码（从 1 起）</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<StockMovementItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? productId,
        StockMovementType? type,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 某商品流水变动量合计（对账 / 一致性校验用：同一商品 Σ Quantity == Inventory.Quantity）
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<int> SumQuantityAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量查询「已发生过库存变动」的商品 id（期初建账限制用，erp-stock-take）：
    /// 在给定商品集合中，返回存在任意流水记录的商品 id 集合（空集合表示全部未发生变动）。
    /// </summary>
    /// <param name="productIds">商品 id 集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已有流水变动的商品 id 集合</returns>
    Task<IReadOnlyCollection<Guid>> GetProductIdsWithMovementsAsync(
        IReadOnlyList<Guid> productIds, CancellationToken cancellationToken = default);
}
