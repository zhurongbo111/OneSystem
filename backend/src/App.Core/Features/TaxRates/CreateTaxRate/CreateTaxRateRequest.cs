using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.TaxRates.CreateTaxRate;

/// <summary>
/// 新增税率请求
/// </summary>
public sealed class CreateTaxRateRequest : IRequest<TaxRateDetailDto>
{
    /// <summary>税率编码（全局唯一）</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>税率名称（全局唯一）</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>税率百分比数值（<c>13</c> 表示 13%）</summary>
    public decimal Rate { get; init; }

    /// <summary>税率状态（0 停用 / 1 启用，默认启用）</summary>
    public int Status { get; init; } = (int)TaxRateStatus.Enabled;

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }
}