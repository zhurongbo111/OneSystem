using App.Core.Abstractions;
using App.Core.Features.GeneralLedger;

namespace App.Core.Features.AccountingPeriods.GetPeriods;

/// <summary>
/// 会计期间列表查询请求（Query 参数绑定；只读）
/// </summary>
public sealed class GetPeriodsRequest : IRequest<IReadOnlyList<PeriodDto>>
{
    /// <summary>年，可空（不传表示全部年份）</summary>
    public int? Year { get; init; }
}
