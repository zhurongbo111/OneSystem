using App.Core.Abstractions;
using App.Core.Features.GeneralLedger;

namespace App.Core.Features.FinancialReports.GetAccountBalance;

/// <summary>
/// 科目余额表查询用例（只读聚合）：期初 / 本期借贷发生额 / 期末余额，按一级科目列示
/// </summary>
public sealed class GetAccountBalanceRequestHandler : IRequestHandler<GetAccountBalanceRequest, IReadOnlyList<AccountBalanceItemDto>>
{
    private readonly IFinancialReportQueryRepository _financialReportQueryRepository;

    /// <summary>
    /// 初始化科目余额表查询用例处理器
    /// </summary>
    public GetAccountBalanceRequestHandler(IFinancialReportQueryRepository financialReportQueryRepository)
    {
        _financialReportQueryRepository = financialReportQueryRepository;
    }

    /// <summary>
    /// 处理科目余额表查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IReadOnlyList<AccountBalanceItemDto>> HandleAsync(GetAccountBalanceRequest request, CancellationToken cancellationToken = default)
    {
        var items = await _financialReportQueryRepository.GetAccountBalancesAsync(request.Year, request.Month, cancellationToken);
        return items.Select(GeneralLedgerDtoMapper.ToAccountBalanceItemDto).ToList();
    }
}
