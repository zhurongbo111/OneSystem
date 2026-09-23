using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 商品仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// Repository 只做数据访问，不做业务判定；一次写操作由方法自身持久化。
/// 列表 / 详情 / 开单选择的库存列由仓储联查 Inventory 带出（低库存标记由 Handler 计算）。
/// </summary>
public interface IProductRepository
{
    /// <summary>
    /// 按 id 查询商品，不存在返回 null（含跟踪，供编辑 / 启停）
    /// </summary>
    /// <param name="id">商品 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 商品编码是否已存在（忽略大小写）；编辑时可排除自身（编码不可改，创建场景恒传 null）
    /// </summary>
    /// <param name="code">商品编码</param>
    /// <param name="excludeId">需要排除的商品 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分页查询商品：按关键词（编码 / 名称模糊）+ 分类 + 状态筛选，创建时间倒序；
    /// 联查 Inventory 带出当前库存
    /// </summary>
    /// <param name="keyword">关键词，可空</param>
    /// <param name="categoryId">分类 id，可空</param>
    /// <param name="status">状态，可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前页数据（含库存）与总条数</returns>
    Task<(IReadOnlyList<ProductListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? categoryId,
        ProductStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询商品详情（联查库存 + 分类名），不存在返回 null
    /// </summary>
    /// <param name="id">商品 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<ProductDetail?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增商品并持久化（库存初始化行由 Handler 经 IInventoryRepository 在同一事务内写入）
    /// </summary>
    /// <param name="product">商品实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(Product product, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新商品并持久化
    /// </summary>
    /// <param name="product">商品实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(Product product, CancellationToken cancellationToken = default);

    /// <summary>
    /// 开单商品选择：仅启用商品，联查当前库存，全量返回（MVP 数据量可控），编码正序。
    /// 038：<paramref name="warehouseId"/> 传仓 → 该仓库存；不传 → 各仓合计（组织级）
    /// </summary>
    /// <param name="warehouseId">仓库 id，可空</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<ProductPickItem>> GetPickListAsync(
        Guid? warehouseId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按商品 id 集合批量查询编码（erp-export 导出单据明细的商品编码列用，一次查询避免 N+1）；
    /// 返回 id → 编码映射，缺失 id 不出现在结果中
    /// </summary>
    /// <param name="productIds">商品 id 集合（空集合返回空字典）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyDictionary<Guid, string>> GetCodesByIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default);
}
