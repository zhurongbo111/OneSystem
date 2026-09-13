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
}
