using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 业务操作审计日志仓储接口（实现见 App.Infrastructure）。
/// 纯追加：只有 <see cref="AddAsync"/> 与查询，不提供更新 / 删除（审计数据不可篡改）。
/// </summary>
public interface IAuditLogRepository
{
    /// <summary>
    /// 追加一条操作日志并持久化（随调用方事务提交）
    /// </summary>
    /// <param name="log">日志实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(AuditLog log, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分页查询操作日志：关键词（业务标识 / 操作人）+ 资源类型 + 动作 + 操作人 id + 时间范围（闭区间）筛选，操作时间倒序。
    /// 列表用投影排除 <c>Changes</c> 大字段，避免拖慢列表
    /// </summary>
    /// <param name="keyword">关键词，匹配 ResourceNo / Username / DisplayName；可空</param>
    /// <param name="resource">资源类型筛选；可空</param>
    /// <param name="action">动作筛选；可空</param>
    /// <param name="userId">操作人 id 筛选；可空</param>
    /// <param name="start">起始操作时间（含）；可空</param>
    /// <param name="end">结束操作时间（含）；可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前页数据与总条数</returns>
    Task<(IReadOnlyList<AuditLog> Items, int Total)> GetPagedAsync(
        string? keyword,
        AuditResource? resource,
        AuditAction? action,
        Guid? userId,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 id 查询单条操作日志（含字段级差异 JSON）
    /// </summary>
    /// <param name="id">日志 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
