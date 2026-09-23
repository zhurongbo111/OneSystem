using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 仓库仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// Repository 只做数据访问，不做业务判定；一次写操作由方法自身持久化。
/// 仓库维度约定（默认仓唯一、只停用不删除等）见 specs/038-erp-multi-warehouse/design.md §0。
/// </summary>
public interface IWarehouseRepository
{
    /// <summary>
    /// 按 id 查询仓库，不存在返回 null（含跟踪，供编辑 / 启停 / 设为默认）
    /// </summary>
    /// <param name="id">仓库 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<Warehouse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 仓库编码是否已存在（忽略大小写）
    /// </summary>
    /// <param name="code">仓库编码</param>
    /// <param name="excludeId">需要排除的仓库 id，可空</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 仓库名称是否已存在（忽略大小写）
    /// </summary>
    /// <param name="name">仓库名称</param>
    /// <param name="excludeId">需要排除的仓库 id，可空</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分页查询仓库：关键词（编码 / 名称模糊）+ 状态筛选，**默认仓置顶**、同组按编码升序
    /// </summary>
    /// <param name="keyword">关键词，可空</param>
    /// <param name="status">状态，可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前页数据与总条数</returns>
    Task<(IReadOnlyList<Warehouse> Items, int Total)> GetPagedAsync(
        string? keyword,
        PartnerStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取全部启用仓（开单下拉用；量小全量返回，按编码升序）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<Warehouse>> GetEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取默认仓（开单未指定仓库时的兜底；不存在属数据异常，由调用方返回 50000）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task<Warehouse?> GetDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增仓库并持久化
    /// </summary>
    /// <param name="warehouse">仓库实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新仓库并持久化
    /// </summary>
    /// <param name="warehouse">仓库实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(Warehouse warehouse, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空其他仓的默认标记（与「设为默认」在同一 IUnitOfWork 事务内调用，保证全局唯一）
    /// </summary>
    /// <param name="exceptId">需要保留默认标记的仓库 id，可空（为空表示清空全部）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ClearDefaultAsync(Guid? exceptId, CancellationToken cancellationToken = default);
}
