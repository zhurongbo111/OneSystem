using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 科目映射仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// 自动凭证按映射取科目；缺失映射由调用方拒绝生成（40158）
/// （specs/033-erp-general-ledger/design.md §3.1）。
/// </summary>
public interface IAccountMappingRepository
{
    /// <summary>
    /// 取全量科目映射（<c>AsNoTracking</c>，一次取回构建映射字典）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<AccountMapping>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增或更新一条映射（按键 upsert）；返回更新后的映射实体
    /// </summary>
    /// <param name="key">映射键</param>
    /// <param name="accountId">目标科目 id</param>
    /// <param name="operatorId">操作人用户 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<AccountMapping> UpsertAsync(string key, Guid accountId, Guid? operatorId, CancellationToken cancellationToken = default);
}
