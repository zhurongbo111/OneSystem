using App.Core.Audit;

namespace App.Core.Abstractions;

/// <summary>
/// 业务操作审计日志写入抽象（实现见 App.Infrastructure）。
/// 调用约定：写用例在业务写完成之后、<c>IUnitOfWork.CommitAsync</c> 之前调用，日志与业务同事务——
/// 业务失败（异常回滚）不产生日志，日志写入失败同样让业务一起失败（见 <c>specs/029-erp-audit-log/design.md</c> §5）。
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// 追加一条操作日志（操作人与快照由实现经 <see cref="ICurrentUser"/> 填充）
    /// </summary>
    /// <param name="entry">日志写入模型</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
