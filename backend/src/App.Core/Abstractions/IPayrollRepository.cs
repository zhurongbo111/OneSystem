using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 月度工资单仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL，044-erp-hcm-payroll）。
/// Repository 只做数据访问，不做业务判定；一次写操作由方法自身持久化。
/// 列表 / 详情为**单表查询、字段与实体 1:1**，故直接返回实体（不建读模型，后端规则 §4.3）。
/// </summary>
public interface IPayrollRepository
{
    /// <summary>
    /// 分页查询工资单：按期间（年 / 月）、员工、状态筛选，创建时间倒序。
    /// </summary>
    /// <param name="year">年，可空</param>
    /// <param name="month">月，可空</param>
    /// <param name="employeeId">员工 id，可空</param>
    /// <param name="status">状态，可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前页实体与总条数</returns>
    Task<(IReadOnlyList<Payroll> Items, int Total)> GetPagedAsync(
        int? year,
        int? month,
        Guid? employeeId,
        PayrollStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 id 查询工资单，不存在返回 null（含跟踪，供编辑 / 发放 / 删除）
    /// </summary>
    /// <param name="id">工资单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<Payroll?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 该员工该期间的工资单是否已存在（唯一性判定，一个员工一个月一条）
    /// </summary>
    /// <param name="employeeId">员工 id</param>
    /// <param name="year">年</param>
    /// <param name="month">月</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsAsync(Guid employeeId, int year, int month, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取该期间可生成工资单的**在职**员工（批量生成用）：状态为在职，且入职日期不晚于该期间月末。
    /// 返回实体（单表查询、字段与实体 1:1，不建读模型）
    /// </summary>
    /// <param name="year">年</param>
    /// <param name="month">月</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<Employee>> GetEmployeesForPeriodAsync(
        int year, int month, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增工资单并持久化
    /// </summary>
    /// <param name="payroll">工资单实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(Payroll payroll, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量新增工资单并持久化（批量生成草稿）
    /// </summary>
    /// <param name="payrolls">工资单实体集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddRangeAsync(IReadOnlyList<Payroll> payrolls, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新工资单并持久化
    /// </summary>
    /// <param name="payroll">工资单实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(Payroll payroll, CancellationToken cancellationToken = default);

    /// <summary>
    /// 物理删除工资单并持久化（仅草稿可删，由 Handler 校验）
    /// </summary>
    /// <param name="payroll">工资单实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteAsync(Payroll payroll, CancellationToken cancellationToken = default);
}
