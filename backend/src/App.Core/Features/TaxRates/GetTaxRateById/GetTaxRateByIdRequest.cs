using App.Core.Abstractions;

namespace App.Core.Features.TaxRates.GetTaxRateById;

/// <summary>
/// 税率详情请求
/// </summary>
public sealed class GetTaxRateByIdRequest : IRequest<TaxRateDetailDto>
{
    /// <summary>税率 id</summary>
    public Guid Id { get; init; }
}