namespace App.Core.Abstractions;

/// <summary>
/// 库存预警只读查询仓储接口（实现见 App.Infrastructure；041-erp-stock-alert）。
/// **只读**：扫描不修改任何业务数据；三类信号的判定口径唯一来源见
/// <c>specs/041-erp-stock-alert/design.md</c> §0（低库存引用 `038` §0，近效期 / 过期引用 `040` §0）。
/// 各方法均受 <c>maxCount</c> 约束，避免异常数据量把一次扫描撑爆。
/// </summary>
public interface IStockAlertQueryRepository
{
    /// <summary>
    /// 低库存信号：按「商品 × 仓」汇总，<c>Σ Quantity &lt; MAX(SafetyStock)</c> 且 <c>MAX(SafetyStock) &gt; 0</c> 且商品启用；
    /// 按数量差额（阈值 − 库存）从大到小返回。
    /// </summary>
    /// <param name="maxCount">单次查询上限</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<StockAlertSignal>> GetLowStockSignalsAsync(
        int maxCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// 近效期批次信号：批次未过期（<c>ExpiryDate &gt;= 今天</c>）且 <c>ExpiryDate &lt;= 今天 + nearDays</c>，
    /// 且该「商品 × 仓 × 批次」库存 &gt; 0；按到期日升序返回。
    /// </summary>
    /// <param name="today">「今天」（UTC 日期粒度，扫描器注入）</param>
    /// <param name="nearDays">近效期窗口天数（取 <c>BatchFieldConstraints.NearExpiryDays</c>）</param>
    /// <param name="maxCount">单次查询上限</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<StockAlertSignal>> GetExpiringBatchSignalsAsync(
        DateOnly today, int nearDays, int maxCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// 已过期批次信号：批次已过期（<c>ExpiryDate &lt; 今天</c>）且该「商品 × 仓 × 批次」库存 &gt; 0；
    /// 按到期日升序返回。
    /// </summary>
    /// <param name="today">「今天」（UTC 日期粒度，扫描器注入）</param>
    /// <param name="maxCount">单次查询上限</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<StockAlertSignal>> GetExpiredBatchSignalsAsync(
        DateOnly today, int maxCount, CancellationToken cancellationToken = default);
}
