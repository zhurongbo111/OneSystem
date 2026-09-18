using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 库存台账仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// 库存与商品 1:1；原子增减用 EF Core ExecuteUpdate 表达（无裸 SQL），
/// 由 erp-purchase（入库 / 作废回冲）与 erp-sale（条件扣减防超卖）消费。
/// </summary>
public interface IInventoryRepository
{
    /// <summary>
    /// 新增库存行（商品新建时初始化 Quantity = 0，与商品写同一事务，由 Handler 用 IUnitOfWork 包裹）
    /// </summary>
    /// <param name="inventory">库存实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(Inventory inventory, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询商品当前库存（无库存行时按 0 计）
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<int> GetQuantityAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 原子增加库存（Quantity = Quantity + delta，delta 允许为负 —— 采购入库 / 采购作废回冲）
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="delta">增量（可正可负）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task IncrementAsync(Guid productId, int delta, CancellationToken cancellationToken = default);

    /// <summary>
    /// 原子条件扣减（Quantity = Quantity - amount WHERE Quantity >= amount，数据库层防超卖）。
    /// 判断与扣减在数据库行锁内原子完成，并发下无需显式行锁 / 乐观版本号。
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="amount">扣减量（正数）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否扣减成功（库存不足时返回 false）</returns>
    Task<bool> TryDecrementAsync(Guid productId, int amount, CancellationToken cancellationToken = default);

    /// <summary>
    /// 原子设定库存为指定值（Quantity = quantity，盘点 / 期初建账按实盘数量校正账面，erp-stock-take）。
    /// 用 EF Core ExecuteUpdate 表达（无裸 SQL），行锁内原子完成；商品无库存行时不产生更新（返回 0 行）。
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="quantity">目标库存（≥ 0）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>受影响的行数（0 表示无库存行，1 表示已设定）</returns>
    Task<int> SetQuantityAsync(Guid productId, int quantity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量读取多个商品的当前库存（无库存行的商品按 0 计，erp-stock-take 读账面用）。
    /// </summary>
    /// <param name="productIds">商品 id 集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>商品 id → 当前库存（缺失商品为 0）</returns>
    Task<IReadOnlyDictionary<Guid, int>> GetQuantitiesAsync(
        IReadOnlyList<Guid> productIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 库存分页查询（erp-inventory-query）：联查 Inventory / Products / Categories，
    /// 仅启用商品（Products.Status = Enabled）；keyword 模糊匹配编码 / 名称；categoryId 精确匹配；
    /// 按 Products.Code 升序。低库存判定不在本方法，由 Handler 计算。
    /// </summary>
    /// <param name="keyword">关键词（编码 / 名称），可空</param>
    /// <param name="categoryId">分类 id，可空</param>
    /// <param name="page">页码（从 1 起）</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<InventoryItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? categoryId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取当前移动加权平均单价（erp-cost；无库存行返回 0）。
    /// 均价是 <c>CostAmount / Quantity</c> 的派生值，只作为读取与兜底用，不作为事实源。
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<decimal> GetAverageCostAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 入库成本写入（erp-cost）：<c>CostAmount += Round(quantity × unitCost, 4)</c> 并按新数量重算 <c>AverageCost</c>；
    /// 调用方**必须先完成数量增加**（先加数量再加金额，否则均价基数错）。数量为 0 时保留最后均价。
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="quantity">入库数量（正数，已计入库存）</param>
    /// <param name="unitCost">本次入库成本单价</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ApplyInboundCostAsync(
        Guid productId,
        int quantity,
        decimal unitCost,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 出库成本结转（erp-cost）：<c>CostAmount −= totalCost</c>，**均价不变**（按均价出库不改变均值）；
    /// 数量归零时成本额归 0 以消除尾差。
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="totalCost">本次出库成本金额（正数，按 变动前均价 × 数量 计算）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ApplyOutboundCostAsync(
        Guid productId,
        decimal totalCost,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 直接设定库存成本两列（erp-cost）：**仅成本重算使用**——重算按流水时序推演出结存金额与均价后一次性写回，
    /// 不参与日常写入路径（日常只允许 <c>ApplyInboundCostAsync</c> / <c>ApplyOutboundCostAsync</c> 增量更新）。
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="costAmount">结存成本额</param>
    /// <param name="averageCost">移动加权平均单价（派生值）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetCostAsync(
        Guid productId,
        decimal costAmount,
        decimal averageCost,
        CancellationToken cancellationToken = default);
}
