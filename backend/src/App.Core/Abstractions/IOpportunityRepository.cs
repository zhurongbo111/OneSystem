using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 商机仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// 商机是**售前意向数据**：不触碰库存、库存流水与收付款（specs/043-erp-crm-presale design.md §1）。
/// </summary>
public interface IOpportunityRepository
{
    /// <summary>
    /// 分页查询商机：单号 / 名称关键词 + 阶段 + 客户 + 负责人筛选，创建时间倒序；
    /// 同时带出负责人姓名（联查 Employees）；客户名称取商机自身的快照列。
    /// </summary>
    /// <param name="keyword">商机单号 / 名称关键词，可空</param>
    /// <param name="stage">商机阶段，可空</param>
    /// <param name="partnerId">关联客户 id，可空</param>
    /// <param name="ownerId">负责人（员工）id，可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<OpportunityListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        OpportunityStage? stage,
        Guid? partnerId,
        Guid? ownerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 id 查询商机详情（商机实体 + 负责人姓名），不存在返回 null。
    /// 详情 / 编辑 / 阶段推进共用本方法取数。
    /// </summary>
    /// <param name="id">商机 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<OpportunityDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增商机并持久化（单号唯一索引冲突时抛 <see cref="Errors.OrderNoConflictException"/>，由 Handler 重试）
    /// </summary>
    /// <param name="opportunity">商机实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(Opportunity opportunity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新商机并持久化（编辑 / 阶段推进共用；审计字段由 Handler 在实体上置好后传入）
    /// </summary>
    /// <param name="opportunity">商机实体（含最新字段）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(Opportunity opportunity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成商机单号：前缀 + yyyyMMdd + 4 位序号（当天同前缀已有商机数 + 1）；
    /// 并发兜底由单号唯一索引承担，冲突重试由 Handler 处理（见 specs/015-erp-purchase design.md §3.6）
    /// </summary>
    /// <param name="prefix">前缀（商机 OP）</param>
    /// <param name="opportunityDate">基准日期（取 UTC 日期段）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<string> GenerateNoAsync(string prefix, DateTimeOffset opportunityDate, CancellationToken cancellationToken = default);
}
