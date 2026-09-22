using App.Core.Abstractions;
using App.Core.Features.GeneralLedger;

namespace App.Core.Features.AccountingPeriods.ClosePeriod;

/// <summary>
/// 会计期间结账请求（路由 id 覆盖请求体 id）
/// </summary>
public sealed class ClosePeriodRequest : IRequest<PeriodDto>
{
    /// <summary>期间 id（取自路由）</summary>
    public Guid Id { get; init; }
}
