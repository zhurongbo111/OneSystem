using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 岗位仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// Repository 只做数据访问，不做业务判定；一次写操作由方法自身持久化。
/// </summary>
public interface IPositionRepository
{
    /// <summary>
    /// 分页查询岗位：按关键词（编码 / 名称模糊）+ 状态筛选，创建时间倒序
    /// </summary>
    /// <param name="keyword">关键词，可空</param>
    /// <param name="status">状态，可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前页数据与总条数</returns>
    Task<(IReadOnlyList<PositionListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        PositionStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 id 查询岗位，不存在返回 null（含跟踪，供编辑 / 启停 / 删除）
    /// </summary>
    /// <param name="id">岗位 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<Position?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 岗位编码是否已存在（忽略大小写）；编辑时可排除自身
    /// </summary>
    /// <param name="code">岗位编码</param>
    /// <param name="excludeId">需要排除的岗位 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 岗位名称是否已存在（忽略大小写）；编辑时可排除自身
    /// </summary>
    /// <param name="name">岗位名称</param>
    /// <param name="excludeId">需要排除的岗位 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 引用该岗位的员工数（删除保护）
    /// </summary>
    /// <param name="positionId">岗位 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<int> CountEmployeesAsync(Guid positionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 岗位选择（仅启用岗位，全量返回；员工表单下拉消费），编码正序
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<PositionPickItem>> GetPickListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增岗位并持久化
    /// </summary>
    /// <param name="position">岗位实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(Position position, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新岗位并持久化
    /// </summary>
    /// <param name="position">岗位实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(Position position, CancellationToken cancellationToken = default);

    /// <summary>
    /// 物理删除岗位（被员工引用时由 Handler 先行拒绝）
    /// </summary>
    /// <param name="position">岗位实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteAsync(Position position, CancellationToken cancellationToken = default);
}
