namespace App.Core.Errors;

/// <summary>
/// 单号唯一约束冲突（技术异常，非业务错误码）。
/// 由仓储在捕获数据库唯一约束异常时抛出，Handler 捕获后重新生成单号重试（最多 3 次，见 design.md §3.6）；
/// 重试耗尽后向上传播，由全局异常中间件按 50000 处理。
/// </summary>
public sealed class OrderNoConflictException : Exception
{
    /// <summary>
    /// 初始化单号冲突异常
    /// </summary>
    /// <param name="innerException">数据库唯一约束异常</param>
    public OrderNoConflictException(Exception innerException)
        : base("单号唯一约束冲突", innerException)
    {
    }
}
