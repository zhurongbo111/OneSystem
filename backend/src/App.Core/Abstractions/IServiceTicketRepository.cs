using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 服务工单仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// 工单是**售后留痕记录**：不做删除（关闭即归档），不触碰库存、库存流水与收付款
/// （specs/045-erp-crm-service design.md §1）。
/// </summary>
public interface IServiceTicketRepository
{
    /// <summary>
    /// 分页查询服务工单：单号 / 客户名 / 标题关键词 + 状态 + 优先级 + 负责人筛选，创建时间倒序；
    /// 同时带出负责人姓名（联查 Employees）。
    /// </summary>
    /// <param name="keyword">工单号 / 客户名 / 标题关键词，可空</param>
    /// <param name="status">工单状态，可空</param>
    /// <param name="priority">优先级，可空</param>
    /// <param name="ownerId">负责人（员工）id，可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<ServiceTicketListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        TicketStatus? status,
        TicketPriority? priority,
        Guid? ownerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 id 查询工单详情（工单实体 + 负责人姓名），不存在返回 null。
    /// 详情 / 编辑 / 状态流转 / 指派共用本方法取数。
    /// </summary>
    /// <param name="id">工单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<ServiceTicketDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增工单并持久化（单号唯一索引冲突时抛 <see cref="Errors.OrderNoConflictException"/>，由 Handler 重试）
    /// </summary>
    /// <param name="ticket">工单实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(ServiceTicket ticket, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新工单并持久化（编辑 / 状态流转 / 指派共用；审计字段由 Handler 在实体上置好后传入）
    /// </summary>
    /// <param name="ticket">工单实体（含最新字段）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(ServiceTicket ticket, CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成工单号：前缀 + yyyyMMdd + 4 位序号（当天同前缀已有工单数 + 1）；
    /// 并发兜底由单号唯一索引承担，冲突重试由 Handler 处理（见 specs/015-erp-purchase design.md §3.6）
    /// </summary>
    /// <param name="prefix">前缀（工单 SV）</param>
    /// <param name="ticketDate">基准日期（取 UTC 日期段）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<string> GenerateNoAsync(string prefix, DateTimeOffset ticketDate, CancellationToken cancellationToken = default);
}
