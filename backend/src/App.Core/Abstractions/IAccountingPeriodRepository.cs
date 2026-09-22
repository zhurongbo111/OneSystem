using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 会计期间仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// Repository 只做数据访问，不做业务判定；期间控制（不存在 40159 / 已结账 40154）在 Handler
/// （specs/033-erp-general-ledger/design.md §3.1）。
/// </summary>
public interface IAccountingPeriodRepository
{
    /// <summary>
    /// 按「年-月」查询期间，不存在返回 <c>null</c>
    /// </summary>
    /// <param name="year">年</param>
    /// <param name="month">月</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<AccountingPeriod?> GetByYearMonthAsync(int year, int month, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 id 查询期间，不存在返回 <c>null</c>（含跟踪，供结账 / 反结账）
    /// </summary>
    /// <param name="id">期间 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<AccountingPeriod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询期间列表（可按年过滤），按年、月升序
    /// </summary>
    /// <param name="year">年，可空（不传表示全部）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<AccountingPeriod>> GetAllAsync(int? year = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 结账 / 反结账（写状态与结账信息）
    /// </summary>
    /// <param name="id">期间 id</param>
    /// <param name="status">目标状态</param>
    /// <param name="operatorId">操作人用户 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetStatusAsync(Guid id, PeriodStatus status, Guid? operatorId, CancellationToken cancellationToken = default);
}
