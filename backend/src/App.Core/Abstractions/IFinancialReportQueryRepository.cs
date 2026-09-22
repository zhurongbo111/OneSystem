namespace App.Core.Abstractions;

/// <summary>
/// 财务报表只读聚合仓储接口（实现见 App.Infrastructure）：跨 Vouchers / VoucherEntries / Accounts 现算，
/// 不落物化表（参照 025 的 <see cref="IReportQueryRepository"/> 组织）
/// （specs/033-erp-general-ledger/design.md §3.1）。
/// </summary>
public interface IFinancialReportQueryRepository
{
    /// <summary>
    /// 科目余额表（按一级科目列示）：期初 / 本期借贷发生额 / 期末余额
    /// </summary>
    /// <param name="year">年</param>
    /// <param name="month">月</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<AccountBalanceItem>> GetAccountBalancesAsync(int year, int month, CancellationToken cancellationToken = default);

    /// <summary>
    /// 资产负债表（按一级科目列示）：资产 / 负债 / 权益三块 + 「本年利润」行（损益类期末净额）
    /// </summary>
    /// <param name="year">年</param>
    /// <param name="month">月</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<BalanceSheetItem>> GetBalanceSheetAsync(int year, int month, CancellationToken cancellationToken = default);

    /// <summary>
    /// 利润表（按一级科目列示）：损益类科目本期发生额，按净额符号分「收入」/「成本费用」两侧
    /// </summary>
    /// <param name="year">年</param>
    /// <param name="month">月</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<IncomeStatementItem>> GetIncomeStatementAsync(int year, int month, CancellationToken cancellationToken = default);
}
