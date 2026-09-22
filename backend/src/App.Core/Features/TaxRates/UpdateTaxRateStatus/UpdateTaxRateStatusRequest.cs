using App.Core.Abstractions;

namespace App.Core.Features.TaxRates.UpdateTaxRateStatus;

/// <summary>
/// 税率停用 / 启用请求（税率只停用不删除，保留历史与发票引用）
/// </summary>
public sealed class UpdateTaxRateStatusRequest : IRequest<TaxRateDetailDto>
{
    /// <summary>税率 id</summary>
    public Guid Id { get; init; }

    /// <summary>目标状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; }
}