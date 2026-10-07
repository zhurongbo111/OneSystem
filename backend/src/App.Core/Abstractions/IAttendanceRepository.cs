using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 考勤登记仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL，044-erp-hcm-payroll）。
/// Repository 只做数据访问，不做业务判定；一次写操作由方法自身持久化。
/// 列表 / 详情为**单表查询、字段与实体 1:1**，故直接返回实体（不建读模型，后端规则 §4.3）。
/// </summary>
public interface IAttendanceRepository
{
    /// <summary>
    /// 分页查询考勤记录：按员工 + 类型 + 日期范围筛选，创建时间倒序。
    /// 日期范围按**区间重叠**判定（记录 <c>StartDate &lt;= endDate</c> 且 <c>EndDate &gt;= startDate</c>）。
    /// </summary>
    /// <param name="employeeId">员工 id，可空</param>
    /// <param name="type">考勤类型，可空</param>
    /// <param name="startDate">筛选起始日，可空</param>
    /// <param name="endDate">筛选结束日，可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前页实体与总条数</returns>
    Task<(IReadOnlyList<Attendance> Items, int Total)> GetPagedAsync(
        Guid? employeeId,
        AttendanceType? type,
        DateOnly? startDate,
        DateOnly? endDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 id 查询考勤记录，不存在返回 null（含跟踪，供编辑 / 删除）
    /// </summary>
    /// <param name="id">考勤记录 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<Attendance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询与给定「员工 + 类型 + 日期区间」重叠的考勤记录（无重叠返回 null）。
    /// 同一员工同一类型的请假 / 加班区间不得重叠（业务判定在 Handler，重叠时 message 含冲突区间）。
    /// </summary>
    /// <param name="employeeId">员工 id</param>
    /// <param name="type">考勤类型</param>
    /// <param name="startDate">起始日</param>
    /// <param name="endDate">结束日</param>
    /// <param name="excludeId">需排除的记录 id（编辑场景排除自身，可空）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<Attendance?> FindOverlapAsync(
        Guid employeeId,
        AttendanceType type,
        DateOnly startDate,
        DateOnly endDate,
        Guid? excludeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增考勤记录并持久化
    /// </summary>
    /// <param name="attendance">考勤实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(Attendance attendance, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新考勤记录并持久化
    /// </summary>
    /// <param name="attendance">考勤实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(Attendance attendance, CancellationToken cancellationToken = default);

    /// <summary>
    /// 物理删除考勤记录并持久化（考勤登记无状态、可删除）
    /// </summary>
    /// <param name="attendance">考勤实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteAsync(Attendance attendance, CancellationToken cancellationToken = default);
}
