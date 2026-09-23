using App.Core.Abstractions;

namespace App.Core.Features.BankAccounts.GetBankAccountSummary;

/// <summary>
/// 资金账户余额总览请求（无参用例，沿用 `GetCurrentUser` 的空请求形态）
/// </summary>
public sealed class GetBankAccountSummaryRequest : IRequest<IReadOnlyList<BankAccountBalanceItemDto>>
{
}
