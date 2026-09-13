using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 商品分类仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// Repository 只做数据访问，不做业务判定；一次写操作由方法自身持久化。
/// </summary>
public interface ICategoryRepository
{
    /// <summary>
    /// 查询全部分类（下拉 / 筛选用，量小全量取），按创建时间正序
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 id 查询分类，不存在返回 null（含跟踪，供编辑 / 删除）
    /// </summary>
    /// <param name="id">分类 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分类名称是否已存在（忽略大小写）；编辑时可排除自身
    /// </summary>
    /// <param name="name">分类名称</param>
    /// <param name="excludeId">需要排除的分类 id（编辑场景传自身 id）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增分类并持久化
    /// </summary>
    /// <param name="category">分类实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(Category category, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新分类并持久化
    /// </summary>
    /// <param name="category">分类实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(Category category, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除分类（仅物理删除分类行，商品引用校验由 Handler 负责）
    /// </summary>
    /// <param name="id">分类 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分类是否被商品引用（删除前校验）
    /// </summary>
    /// <param name="id">分类 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ReferencedByProductsAsync(Guid id, CancellationToken cancellationToken = default);
}
