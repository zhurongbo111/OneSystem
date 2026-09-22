using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 会计科目仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// Repository 只做数据访问，不做业务判定；一次写操作由方法自身持久化。
/// 树形由 <c>ParentId</c> 自引用表达，防环 / 删除保护等判定在 Handler
/// （specs/031-erp-finance-master/design.md §3.1）。
/// </summary>
public interface IAccountRepository
{
    /// <summary>
    /// 取全量会计科目（<c>AsNoTracking</c>，含停用科目：树需完整展示，停用仅影响可选性），
    /// 由用例内存建树
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 id 查询科目，不存在返回 null（含跟踪，供编辑 / 启停 / 删除）
    /// </summary>
    /// <param name="id">科目 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 科目编码是否已存在（忽略大小写）；编辑时可排除自身
    /// </summary>
    /// <param name="code">科目编码</param>
    /// <param name="excludeId">需要排除的科目 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 科目是否存在子科目（删除保护）
    /// </summary>
    /// <param name="id">科目 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 科目是否已被凭证分录引用（删除保护）；凭证表由 `033-erp-general-ledger` 落地，
    /// 当前无凭证表，固定返回 <c>false</c>，`033` 落地后改为真实查询
    /// </summary>
    /// <param name="id">科目 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> IsReferencedByVoucherAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增科目并持久化
    /// </summary>
    /// <param name="account">科目实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(Account account, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新科目并持久化
    /// </summary>
    /// <param name="account">科目实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(Account account, CancellationToken cancellationToken = default);

    /// <summary>
    /// 物理删除科目（有子科目或被凭证引用时由 Handler 先行拒绝）
    /// </summary>
    /// <param name="account">科目实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteAsync(Account account, CancellationToken cancellationToken = default);
}