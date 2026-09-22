using App.Core.Abstractions;

namespace App.Core.Features.TaxRates.DeleteTaxRate;

/// <summary>
/// 删除税率请求
/// </summary>
public sealed class DeleteTaxRateRequest : IRequest<object?>
{
    /// <summary>税率 id</summary>
    public Guid Id { get; init; }
}