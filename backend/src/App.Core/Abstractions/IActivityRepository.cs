using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 跟进活动仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// 活动是**纯追加表**：只增不改不删（跟进留痕，specs/043-erp-crm-presale design.md §0.1 / §5），故无更新 / 删除方法。
/// </summary>
public interface IActivityRepository
{
    /// <summary>
    /// 按归属（业务类型 + 业务 id）查询跟进活动，跟进时间倒序（同一时间按记录顺序倒序）；
    /// 同时带出记录人显示名（联查 Users）。
    /// </summary>
    /// <param name="bizType">归属业务类型（线索 / 商机）</param>
    /// <param name="bizId">归属业务 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<ActivityItem>> GetByBizAsync(
        ActivityBizType bizType,
        Guid bizId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增跟进活动并持久化（只增；归属业务是否存在由 Handler 校验）
    /// </summary>
    /// <param name="activity">活动实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(Activity activity, CancellationToken cancellationToken = default);
}
