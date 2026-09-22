using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 税率仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// Repository 只做数据访问，不做业务判定；一次写操作由方法自身持久化
/// （specs/031-erp-finance-master/design.md §3.1）。
/// </summary>
public interface ITaxRateRepository
{
    /// <summary>
    /// 分页查询税率：按关键词（编码 / 名称模糊）+ 状态筛选，创建时间倒序
    /// </summary>
    /// <param name="keyword">关键词，可空</param>
    /// <param name="status">状态，可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前页数据与总条数</returns>
    Task<(IReadOnlyList<TaxRateListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        TaxRateStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 id 查询税率，不存在返回 null（含跟踪，供编辑 / 启停 / 删除）
    /// </summary>
    /// <param name="id">税率 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<TaxRate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 税率编码是否已存在（忽略大小写）；编辑时可排除自身
    /// </summary>
    /// <param name="code">税率编码</param>
    /// <param name="excludeId">需要排除的税率 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 税率名称是否已存在（忽略大小写）；编辑时可排除自身
    /// </summary>
    /// <param name="name">税率名称</param>
    /// <param name="excludeId">需要排除的税率 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增税率并持久化
    /// </summary>
    /// <param name="taxRate">税率实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(TaxRate taxRate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新税率并持久化
    /// </summary>
    /// <param name="taxRate">税率实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(TaxRate taxRate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 物理删除税率
    /// </summary>
    /// <param name="taxRate">税率实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteAsync(TaxRate taxRate, CancellationToken cancellationToken = default);
}