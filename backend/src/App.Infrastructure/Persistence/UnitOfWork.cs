using App.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace App.Infrastructure.Persistence;

/// <summary>
/// 基于 DbContext 的事务工作单元实现：为跨仓储写操作提供显式事务边界
/// </summary>
internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _dbContext;
    private IDbContextTransaction? _transaction;

    /// <summary>
    /// 初始化工作单元
    /// </summary>
    public UnitOfWork(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// 开启事务；已有未提交的事务时抛异常（fail-fast，禁止重复开启）
    /// </summary>
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException(
                "当前工作单元已有未提交的事务，禁止重复开启；请先 CommitAsync 或 RollbackAsync。");
        }

        _transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    /// <summary>
    /// 提交当前事务；未开启事务时忽略
    /// </summary>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    /// <summary>
    /// 回滚当前事务；未开启事务时忽略
    /// </summary>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    /// <summary>
    /// 释放未提交 / 未回滚的事务
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}
