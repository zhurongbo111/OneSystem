using App.Core.Abstractions;
using App.Core.Features.GeneralLedger;

namespace App.Core.Features.FinancialReports.GetIncomeStatement;

/// <summary>
/// 利润表查询用例（只读聚合）：损益类科目本期发生额，按净额符号分「收入」/「成本费用」两侧；
/// 净利润 = 收入合计 − 成本费用合计（与 `026` 成本毛利报表同源口径）
/// </summary>
public sealed class GetIncomeStatementRequestHandler : IRequestHandler<GetIncomeStatementRequest, IncomeStatementDto>
{
    private readonly IFinancialReportQueryRepository _financialReportQueryRepository;

    /// <summary>
    /// 初始化利润表查询用例处理器
    /// </summary>
    public GetIncomeStatementRequestHandler(IFinancialReportQueryRepository financialReportQueryRepository)
    {
        _financialReportQueryRepository = financialReportQueryRepository;
    }

    /// <summary>
    /// 处理利润表查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IncomeStatementDto> HandleAsync(GetIncomeStatementRequest request, CancellationToken cancellationToken = default)
    {
        var items = await _financialReportQueryRepository.GetIncomeStatementAsync(request.Year, request.Month, cancellationToken);

        var revenueItems = items.Where(i => i.IsRevenue).ToList();
        var costItems = items.Where(i => !i.IsRevenue).ToList();
        var totalRevenue = revenueItems.Sum(i => i.Amount);
        var totalCost = costItems.Sum(i => i.Amount);

        return new IncomeStatementDto
        {
            Year = request.Year,
            Month = request.Month,
            RevenueItems = revenueItems.Select(GeneralLedgerDtoMapper.ToIncomeStatementItemDto).ToList(),
            CostItems = costItems.Select(GeneralLedgerDtoMapper.ToIncomeStatementItemDto).ToList(),
            TotalRevenue = totalRevenue,
            TotalCost = totalCost,
            NetProfit = totalRevenue - totalCost,
        };
    }
}
