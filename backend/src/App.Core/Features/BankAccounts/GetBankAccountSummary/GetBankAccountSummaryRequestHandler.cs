using App.Core.Abstractions;

namespace App.Core.Features.BankAccounts.GetBankAccountSummary;

/// <summary>
/// 资金账户余额总览用例：返回各账户当前余额（初始余额 + Σ 收款 − Σ 付款，只计未作废收付款单）。
/// 余额为派生值、不入账户列（specs/034-erp-cash/design.md §0.2）
/// </summary>
public sealed class GetBankAccountSummaryRequestHandler : IRequestHandler<GetBankAccountSummaryRequest, IReadOnlyList<BankAccountBalanceItemDto>>
{
    private readonly IBankAccountRepository _bankAccountRepository;

    /// <summary>
    /// 初始化资金账户余额总览用例处理器
    /// </summary>
    public GetBankAccountSummaryRequestHandler(IBankAccountRepository bankAccountRepository)
    {
        _bankAccountRepository = bankAccountRepository;
    }

    /// <summary>
    /// 处理资金账户余额总览请求
    /// </summary>
    /// <param name="request">总览请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IReadOnlyList<BankAccountBalanceItemDto>> HandleAsync(
        GetBankAccountSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        var balances = await _bankAccountRepository.GetBalancesAsync(cancellationToken);
        return balances.Select(BankAccountDtoMapper.ToBankAccountBalanceItemDto).ToList();
    }
}
