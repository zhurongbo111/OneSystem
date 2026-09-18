namespace App.Core.Features.Costs;

/// <summary>
/// 成本重算的进程内互斥锁（erp-cost design §5）：单实例部署下用内存锁拒绝并发重算（返回 <c>40118</c>）。
/// 注册为 Singleton，由 <c>RecalculateCosts</c> 用例在 <c>try/finally</c> 中获取与释放，
/// 保证异常路径也能释放（不引入数据库 advisory lock，跨实例场景另议）。
/// </summary>
public sealed class CostRecalculationLock
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>尝试进入重算临界区；已在执行时返回 false（不等待，便于即时拒绝）</summary>
    public bool TryEnter() => _semaphore.Wait(0);

    /// <summary>释放重算临界区</summary>
    public void Release() => _semaphore.Release();
}
