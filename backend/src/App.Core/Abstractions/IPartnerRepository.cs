using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 往来单位仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// Repository 只做数据访问，不做业务判定；一次写操作由方法自身持久化。
/// </summary>
public interface IPartnerRepository
{
    /// <summary>
    /// 按 id 查询往来单位，不存在返回 null（含跟踪，供编辑 / 启停）
    /// </summary>
    /// <param name="id">往来单位 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<Partner?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 单位名称是否已存在（忽略大小写）；编辑时名称不可改，恒传 null
    /// </summary>
    /// <param name="name">单位名称</param>
    /// <param name="excludeId">需要排除的往来单位 id，可空</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分页查询往来单位：按关键词（名称 / 联系人模糊）+ 类型 + 状态筛选，创建时间倒序
    /// </summary>
    /// <param name="keyword">关键词，可空</param>
    /// <param name="type">单位类型，可空</param>
    /// <param name="status">状态，可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前页数据与总条数</returns>
    Task<(IReadOnlyList<Partner> Items, int Total)> GetPagedAsync(
        string? keyword,
        PartnerType? type,
        PartnerStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增往来单位并持久化
    /// </summary>
    /// <param name="partner">往来单位实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(Partner partner, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新往来单位并持久化
    /// </summary>
    /// <param name="partner">往来单位实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(Partner partner, CancellationToken cancellationToken = default);
}
