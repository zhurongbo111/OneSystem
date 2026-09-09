namespace App.Core.Abstractions;

/// <summary>
/// 工作单元：为 RequestHandler 内跨多个仓储的写操作提供显式事务边界。
/// 接口定义在 App.Core，实现（基于 DbContext）在 App.Infrastructure。
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>开启事务</summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>提交当前事务（成功后事务对象即失效）</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>回滚当前事务（成功后事务对象即失效）</summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
