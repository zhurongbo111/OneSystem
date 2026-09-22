using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 员工仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// Repository 只做数据访问，不做业务判定；一次写操作由方法自身持久化。
/// 员工列表 / 详情的部门名、岗位名、关联账号显示名由仓储联查带出
/// （specs/030-erp-org-employee/design.md §3.1）。
/// </summary>
public interface IEmployeeRepository
{
    /// <summary>
    /// 分页查询员工：按关键词（工号 / 姓名模糊）+ 部门 + 岗位 + 在职状态筛选，创建时间倒序
    /// </summary>
    /// <param name="keyword">关键词，可空</param>
    /// <param name="departmentId">部门 id，可空</param>
    /// <param name="positionId">岗位 id，可空</param>
    /// <param name="status">在职状态，可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前页数据与总条数</returns>
    Task<(IReadOnlyList<EmployeeListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? departmentId,
        Guid? positionId,
        EmployeeStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按筛选条件取导出用数据（同列表口径，不分页），最多取 <paramref name="limit"/> 行
    /// </summary>
    /// <param name="keyword">关键词，可空</param>
    /// <param name="departmentId">部门 id，可空</param>
    /// <param name="positionId">岗位 id，可空</param>
    /// <param name="status">在职状态，可空</param>
    /// <param name="limit">取数上限（导出按 MaxRows + 1 判定超限）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<EmployeeListItem>> GetAllForExportAsync(
        string? keyword,
        Guid? departmentId,
        Guid? positionId,
        EmployeeStatus? status,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 id 查询员工，不存在返回 null（含跟踪，供编辑 / 启停）
    /// </summary>
    /// <param name="id">员工 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询员工详情（联查部门 / 岗位 / 关联账号名称），不存在返回 null
    /// </summary>
    /// <param name="id">员工 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<EmployeeDetail?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 工号是否已存在（忽略大小写）；编辑时可排除自身
    /// </summary>
    /// <param name="employeeNo">工号</param>
    /// <param name="excludeId">需要排除的员工 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsByNoAsync(string employeeNo, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 手机号是否已被占用（忽略大小写）；编辑时可排除自身
    /// </summary>
    /// <param name="phone">手机号</param>
    /// <param name="excludeId">需要排除的员工 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsByPhoneAsync(string phone, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 邮箱是否已被占用（忽略大小写）；编辑时可排除自身
    /// </summary>
    /// <param name="email">邮箱</param>
    /// <param name="excludeId">需要排除的员工 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 账号是否已被其他员工绑定（一个账号最多绑一个员工）；编辑时可排除自身
    /// </summary>
    /// <param name="userId">账号 id</param>
    /// <param name="excludeId">需要排除的员工 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsByUserIdAsync(Guid userId, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 可选账号来源：启用且未被绑定的账号 ∪ 指定员工当前已绑定的账号，登录名正序
    /// </summary>
    /// <param name="employeeId">当前员工 id（新增场景传 null）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<EmployeePickUserItem>> GetAvailableUsersAsync(Guid? employeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增员工并持久化
    /// </summary>
    /// <param name="employee">员工实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(Employee employee, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新员工并持久化
    /// </summary>
    /// <param name="employee">员工实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(Employee employee, CancellationToken cancellationToken = default);
}
