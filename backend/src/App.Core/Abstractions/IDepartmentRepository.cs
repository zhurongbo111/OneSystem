using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 部门仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// Repository 只做数据访问，不做业务判定；一次写操作由方法自身持久化。
/// 树形由 <c>ParentId</c> 自引用表达，防环 / 删除保护等判定在 Handler
/// （specs/030-erp-org-employee/design.md §3.4）。
/// </summary>
public interface IDepartmentRepository
{
    /// <summary>
    /// 取全量部门并组装为树（同级按排序升序，含每个部门的在职员工数），返回顶级节点集合
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<DepartmentTreeNode>> GetTreeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 id 查询部门，不存在返回 null（含跟踪，供编辑 / 启停 / 删除）
    /// </summary>
    /// <param name="id">部门 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 部门编码是否已存在（忽略大小写）；编辑时可排除自身
    /// </summary>
    /// <param name="code">部门编码</param>
    /// <param name="excludeId">需要排除的部门 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 同一上级下部门名称是否已存在（忽略大小写）；编辑时可排除自身
    /// </summary>
    /// <param name="name">部门名称</param>
    /// <param name="parentId">上级部门 id（<c>null</c> 表示顶级）</param>
    /// <param name="excludeId">需要排除的部门 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsByNameAsync(string name, Guid? parentId, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 部门是否存在子部门（删除保护）
    /// </summary>
    /// <param name="id">部门 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量取各部门的**在职**员工数（部门 id → 人数；无人数的部门不出现在结果中）；部门树「在职人数」列用
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyDictionary<Guid, int>> GetEmployeeCountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 引用该部门的员工数（**含离职**，与员工表的部门外键一致）；删除保护判据用
    /// </summary>
    /// <param name="departmentId">部门 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<int> CountEmployeesAsync(Guid departmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增部门并持久化
    /// </summary>
    /// <param name="department">部门实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(Department department, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新部门并持久化
    /// </summary>
    /// <param name="department">部门实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(Department department, CancellationToken cancellationToken = default);

    /// <summary>
    /// 物理删除部门（有子部门或员工引用时由 Handler 先行拒绝）
    /// </summary>
    /// <param name="department">部门实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteAsync(Department department, CancellationToken cancellationToken = default);
}
