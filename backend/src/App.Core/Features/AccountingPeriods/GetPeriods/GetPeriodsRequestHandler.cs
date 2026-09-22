using App.Core.Abstractions;
using App.Core.Features.GeneralLedger;

namespace App.Core.Features.AccountingPeriods.GetPeriods;

/// <summary>
/// 会计期间列表查询用例：仓储按年筛选（可空）→ 映射出参
/// </summary>
public sealed class GetPeriodsRequestHandler : IRequestHandler<GetPeriodsRequest, IReadOnlyList<PeriodDto>>
{
    private readonly IAccountingPeriodRepository _accountingPeriodRepository;

    /// <summary>
    /// 初始化会计期间列表查询用例处理器
    /// </summary>
    public GetPeriodsRequestHandler(IAccountingPeriodRepository accountingPeriodRepository)
    {
        _accountingPeriodRepository = accountingPeriodRepository;
    }

    /// <summary>
    /// 处理会计期间列表查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IReadOnlyList<PeriodDto>> HandleAsync(GetPeriodsRequest request, CancellationToken cancellationToken = default)
    {
        var periods = await _accountingPeriodRepository.GetAllAsync(request.Year, cancellationToken);
        return periods.Select(GeneralLedgerDtoMapper.ToPeriodDto).ToList();
    }
}
