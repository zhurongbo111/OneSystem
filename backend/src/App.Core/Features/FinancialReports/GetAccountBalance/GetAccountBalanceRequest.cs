using App.Core.Abstractions;
using App.Core.Features.GeneralLedger;

namespace App.Core.Features.FinancialReports.GetAccountBalance;

/// <summary>
/// 科目余额表查询请求（按期间；Query 参数绑定）
/// </summary>
public sealed class GetAccountBalanceRequest : IRequest<IReadOnlyList<AccountBalanceItemDto>>
{
    /// <summary>年</summary>
    public int Year { get; init; }

    /// <summary>月（1–12）</summary>
    public int Month { get; init; }
}
