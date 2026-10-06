using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 线索仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// 线索是**售前意向数据**：不触碰库存、库存流水与收付款（specs/043-erp-crm-presale design.md §1）；
/// 转商机（建商机 + 回写线索）由 Handler 用 IUnitOfWork 包成同一事务。
/// </summary>
public interface ILeadRepository
{
    /// <summary>
    /// 分页查询线索：单号 / 名称 / 联系人 / 电话关键词 + 来源 + 状态 + 负责人筛选，创建时间倒序；
    /// 同时带出负责人姓名（联查 Employees）。
    /// </summary>
    /// <param name="keyword">线索单号 / 名称 / 联系人 / 电话关键词，可空</param>
    /// <param name="source">线索来源，可空</param>
    /// <param name="status">线索状态，可空</param>
    /// <param name="ownerId">负责人（员工）id，可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<LeadListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        LeadSource? source,
        LeadStatus? status,
        Guid? ownerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 id 查询线索详情（线索实体 + 负责人姓名），不存在返回 null。
    /// 详情 / 编辑 / 状态流转 / 转商机共用本方法取数。
    /// </summary>
    /// <param name="id">线索 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<LeadDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增线索并持久化（单号唯一索引冲突时抛 <see cref="Errors.OrderNoConflictException"/>，由 Handler 重试）
    /// </summary>
    /// <param name="lead">线索实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(Lead lead, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新线索并持久化（编辑 / 状态流转 / 转商机回写共用；审计字段由 Handler 在实体上置好后传入）
    /// </summary>
    /// <param name="lead">线索实体（含最新字段）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(Lead lead, CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成线索单号：前缀 + yyyyMMdd + 4 位序号（当天同前缀已有线索数 + 1）；
    /// 并发兜底由单号唯一索引承担，冲突重试由 Handler 处理（见 specs/015-erp-purchase design.md §3.6）
    /// </summary>
    /// <param name="prefix">前缀（线索 LD）</param>
    /// <param name="leadDate">基准日期（取 UTC 日期段）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<string> GenerateNoAsync(string prefix, DateTimeOffset leadDate, CancellationToken cancellationToken = default);
}
