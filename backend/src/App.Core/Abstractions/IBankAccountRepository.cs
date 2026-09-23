using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 资金账户仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// Repository 只做数据访问，不做业务判定；一次写操作由方法自身持久化
/// （specs/034-erp-cash/design.md §3.1）。
/// </summary>
public interface IBankAccountRepository
{
    /// <summary>
    /// 分页查询资金账户：按关键词（编码 / 名称模糊）+ 类型 + 状态筛选，创建时间倒序；
    /// 余额列由收付款单聚合派生（初始余额 + Σ 收款 − Σ 付款，只计未作废单据）
    /// </summary>
    /// <param name="keyword">关键词，可空</param>
    /// <param name="type">账户类型，可空</param>
    /// <param name="status">状态，可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前页数据与总条数</returns>
    Task<(IReadOnlyList<BankAccountListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        BankAccountType? type,
        BankAccountStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 id 查询资金账户，不存在返回 null（含跟踪，供编辑 / 启停 / 删除）
    /// </summary>
    /// <param name="id">资金账户 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<BankAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 账户编码是否已存在（忽略大小写）；编辑时可排除自身
    /// </summary>
    /// <param name="code">账户编码</param>
    /// <param name="excludeId">需要排除的账户 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量查询各资金账户当前余额（初始余额 + Σ 收款 − Σ 付款，只计未作废收付款单）；无账户时返回空列表
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<BankAccountBalanceItem>> GetBalancesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 资金账户是否已被收付款单引用（删除保护）
    /// </summary>
    /// <param name="id">资金账户 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> IsReferencedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增资金账户并持久化
    /// </summary>
    /// <param name="bankAccount">资金账户实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(BankAccount bankAccount, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新资金账户并持久化
    /// </summary>
    /// <param name="bankAccount">资金账户实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(BankAccount bankAccount, CancellationToken cancellationToken = default);

    /// <summary>
    /// 物理删除资金账户
    /// </summary>
    /// <param name="bankAccount">资金账户实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteAsync(BankAccount bankAccount, CancellationToken cancellationToken = default);
}
