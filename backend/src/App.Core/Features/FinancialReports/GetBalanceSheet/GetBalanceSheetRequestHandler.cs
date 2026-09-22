using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Features.GeneralLedger;

namespace App.Core.Features.FinancialReports.GetBalanceSheet;

/// <summary>
/// 资产负债表查询用例（只读聚合）：资产（含成本类，生产成本属存货性质）/ 负债 / 权益三块 +
/// 损益类净额构成的「本年利润」行；恒等式「资产 = 负债 + 权益 + 本年利润」由单测守护
/// </summary>
public sealed class GetBalanceSheetRequestHandler : IRequestHandler<GetBalanceSheetRequest, BalanceSheetDto>
{
    private readonly IFinancialReportQueryRepository _financialReportQueryRepository;

    /// <summary>
    /// 初始化资产负债表查询用例处理器
    /// </summary>
    public GetBalanceSheetRequestHandler(IFinancialReportQueryRepository financialReportQueryRepository)
    {
        _financialReportQueryRepository = financialReportQueryRepository;
    }

    /// <summary>
    /// 处理资产负债表查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<BalanceSheetDto> HandleAsync(GetBalanceSheetRequest request, CancellationToken cancellationToken = default)
    {
        var items = await _financialReportQueryRepository.GetBalanceSheetAsync(request.Year, request.Month, cancellationToken);

        // 成本类（如生产成本）视同资产侧：保证「资产 = 负债 + 权益 + 本年利润」恒等
        var assets = items.Where(i => i.Category is AccountCategory.Asset or AccountCategory.Cost).ToList();
        var liabilities = items.Where(i => i.Category == AccountCategory.Liability).ToList();
        var equities = items.Where(i => i.Category == AccountCategory.Equity).ToList();
        var profitLossItems = items.Where(i => i.Category == AccountCategory.ProfitLoss).ToList();

        var totalAssets = assets.Sum(i => i.Amount);
        var totalLiabilities = liabilities.Sum(i => i.Amount);
        var totalEquities = equities.Sum(i => i.Amount);
        var currentProfit = profitLossItems.Sum(i => i.Amount);

        return new BalanceSheetDto
        {
            Year = request.Year,
            Month = request.Month,
            Assets = assets.Select(GeneralLedgerDtoMapper.ToBalanceSheetItemDto).ToList(),
            Liabilities = liabilities.Select(GeneralLedgerDtoMapper.ToBalanceSheetItemDto).ToList(),
            Equities = equities.Select(GeneralLedgerDtoMapper.ToBalanceSheetItemDto).ToList(),
            ProfitLossItems = profitLossItems.Select(GeneralLedgerDtoMapper.ToBalanceSheetItemDto).ToList(),
            TotalAssets = totalAssets,
            TotalLiabilities = totalLiabilities,
            TotalEquities = totalEquities,
            CurrentProfit = currentProfit,
            TotalLiabilitiesAndEquity = totalLiabilities + totalEquities + currentProfit,
        };
    }
}
