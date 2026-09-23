namespace App.Core.Abstractions;

/// <summary>
/// 库存台账仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// 库存的定位维度是**商品 × 仓库**（<c>(ProductId, WarehouseId)</c> 唯一，specs/038-erp-multi-warehouse/design.md §0）；
/// 原子增减用 EF Core ExecuteUpdate 表达（无裸 SQL），
/// 由 erp-purchase（入库 / 作废回冲）、erp-sale（条件扣减防超卖）、erp-stock-take（盘点按仓设定）消费。
/// </summary>
public interface IInventoryRepository
{
    /// <summary>
    /// 确保「商品 × 仓库」库存行存在（不存在则新建 <c>Quantity = 0</c>，安全库存取入参初始值）。
    /// 商品新建时为每个启用仓逐仓调用（与商品写同一事务，由 Handler 用 IUnitOfWork 包裹）。
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="warehouseId">仓库 id</param>
    /// <param name="safetyStock">该仓安全库存初始值（取商品档案阈值）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task EnsureRowAsync(
        Guid productId, Guid warehouseId, int safetyStock, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询商品在某仓的当前库存（无库存行时按 0 计）
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="warehouseId">仓库 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<int> GetQuantityAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询商品的**全组织库存合计**（Σ 各仓库存；商品停用判断等组织级场景用）
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<int> GetTotalQuantityAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 原子增加某仓库存（<c>Quantity = Quantity + delta</c>，delta 允许为负 —— 采购入库 / 采购作废回冲）。
    /// 该仓无库存行且 delta 为正时先建行（安全库存取商品档案阈值）再累加（仓后建场景）。
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="warehouseId">仓库 id</param>
    /// <param name="delta">增量（可正可负）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task IncrementAsync(
        Guid productId, Guid warehouseId, int delta, CancellationToken cancellationToken = default);

    /// <summary>
    /// 原子条件扣减某仓库存（<c>Quantity = Quantity - amount WHERE Quantity &gt;= amount</c>，数据库层防超卖）。
    /// 判断与扣减在数据库行锁内原子完成，并发下无需显式行锁 / 乐观版本号；该仓无库存行时返回 false（视为 0）。
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="warehouseId">仓库 id</param>
    /// <param name="amount">扣减量（正数）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否扣减成功（该仓库存不足时返回 false）</returns>
    Task<bool> TryDecrementAsync(
        Guid productId, Guid warehouseId, int amount, CancellationToken cancellationToken = default);

    /// <summary>
    /// 原子设定某仓库存为指定值（盘点 / 期初建账按实盘数量校正账面，erp-stock-take）。
    /// 用 EF Core ExecuteUpdate 表达（无裸 SQL），行锁内原子完成；该仓无库存行时不产生更新（返回 0 行）。
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="warehouseId">仓库 id</param>
    /// <param name="quantity">目标库存（≥ 0）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>受影响的行数（0 表示该仓无库存行，1 表示已设定）</returns>
    Task<int> SetQuantityAsync(
        Guid productId, Guid warehouseId, int quantity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量读取多个商品在某仓的当前库存（无库存行的商品按 0 计，erp-stock-take 读所选仓账面用）。
    /// </summary>
    /// <param name="warehouseId">仓库 id</param>
    /// <param name="productIds">商品 id 集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>商品 id → 该仓当前库存（缺失商品为 0）</returns>
    Task<IReadOnlyDictionary<Guid, int>> GetQuantitiesAsync(
        Guid warehouseId, IReadOnlyList<Guid> productIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 库存分页查询（erp-inventory-query）：联查 Inventory / Products / Categories / Warehouses，
    /// 仅启用商品（Products.Status = Enabled）；keyword 模糊匹配编码 / 名称；categoryId 精确匹配；
    /// warehouseId 精确匹配（可空 = 全部仓，038）；按 Products.Code 升序。低库存判定不在本方法，由 Handler 计算。
    /// </summary>
    /// <param name="keyword">关键词（编码 / 名称），可空</param>
    /// <param name="categoryId">分类 id，可空</param>
    /// <param name="warehouseId">仓库 id，可空（不传 = 全部仓）</param>
    /// <param name="page">页码（从 1 起）</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<InventoryItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? categoryId,
        Guid? warehouseId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 维护「商品 × 仓库」的仓级安全库存阈值（低库存判定的唯一来源）
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="warehouseId">仓库 id</param>
    /// <param name="safetyStock">安全库存阈值（0 表示不提醒）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>受影响的行数（0 表示该仓无库存行）</returns>
    Task<int> UpdateSafetyStockAsync(
        Guid productId, Guid warehouseId, int safetyStock, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取商品在某仓的当前移动加权平均单价（erp-cost；无库存行返回 0）。
    /// 均价是 <c>CostAmount / Quantity</c> 的派生值，只作为读取与兜底用，不作为事实源。
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="warehouseId">仓库 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<decimal> GetAverageCostAsync(
        Guid productId, Guid warehouseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 入库成本写入（erp-cost）：<c>CostAmount += Round(quantity × unitCost, 4)</c> 并按新数量重算 <c>AverageCost</c>；
    /// 调用方**必须先完成数量增加**（先加数量再加金额，否则均价基数错）。数量为 0 时保留最后均价。
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="warehouseId">仓库 id</param>
    /// <param name="quantity">入库数量（正数，已计入该仓库存）</param>
    /// <param name="unitCost">本次入库成本单价</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ApplyInboundCostAsync(
        Guid productId,
        Guid warehouseId,
        int quantity,
        decimal unitCost,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 出库成本结转（erp-cost）：<c>CostAmount −= totalCost</c>，**均价不变**（按均价出库不改变均值）；
    /// 数量归零时成本额归 0 以消除尾差。
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="warehouseId">仓库 id</param>
    /// <param name="totalCost">本次出库成本金额（正数，按 变动前均价 × 数量 计算）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ApplyOutboundCostAsync(
        Guid productId, Guid warehouseId, decimal totalCost, CancellationToken cancellationToken = default);

    /// <summary>
    /// 直接设定某仓库存成本两列（erp-cost）：**仅成本重算使用**——重算按流水时序推演出结存金额与均价后一次性写回，
    /// 不参与日常写入路径（日常只允许 <c>ApplyInboundCostAsync</c> / <c>ApplyOutboundCostAsync</c> 增量更新）。
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="warehouseId">仓库 id</param>
    /// <param name="costAmount">结存成本额</param>
    /// <param name="averageCost">移动加权平均单价（派生值）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetCostAsync(
        Guid productId,
        Guid warehouseId,
        decimal costAmount,
        decimal averageCost,
        CancellationToken cancellationToken = default);
}
