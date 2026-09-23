namespace App.Core.Abstractions;

/// <summary>
/// 资金日记账只读聚合查询仓储接口（实现见 App.Infrastructure；只做跨 <c>BankAccounts</c> / <c>Settlements</c> 的读取）
/// （specs/034-erp-cash/design.md §3.1）。资金日记账由收付款单派生，不落流水表。
/// </summary>
public interface ICashJournalQueryRepository
{
    /// <summary>
    /// 查询指定资金账户在闭区间内的资金日记账：
    /// 期初 = 初始余额 + 起始日之前的收付款净额；流水按业务日期升序（收款为正、付款为负）
    /// </summary>
    /// <param name="bankAccountId">资金账户 id</param>
    /// <param name="start">起始业务日期（含）</param>
    /// <param name="end">结束业务日期（含）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>期初余额与区间内流水；账户不存在时返回 (false, null, 空集合)</returns>
    Task<(bool Exists, decimal OpeningBalance, IReadOnlyList<CashJournalEntryItem> Entries)> GetJournalAsync(
        Guid bankAccountId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default);
}
