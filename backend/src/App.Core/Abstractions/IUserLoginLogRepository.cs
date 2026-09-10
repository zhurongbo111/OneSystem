using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 用户登录日志仓储接口（实现见 App.Infrastructure）。
/// 日志只追加与查询，不提供修改 / 删除（审计数据不可篡改）。
/// </summary>
public interface IUserLoginLogRepository
{
    /// <summary>
    /// 追加一条登录日志并持久化
    /// </summary>
    /// <param name="log">登录日志实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(UserLoginLog log, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分页查询登录日志：登录名模糊 + 登录时间范围（闭区间）筛选，登录时间倒序
    /// </summary>
    /// <param name="username">登录名关键词，可空</param>
    /// <param name="startTime">起始时间（含），可空</param>
    /// <param name="endTime">结束时间（含），可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前页数据与总条数</returns>
    Task<(IReadOnlyList<UserLoginLog> Items, int Total)> GetPagedAsync(
        string? username,
        DateTimeOffset? startTime,
        DateTimeOffset? endTime,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
