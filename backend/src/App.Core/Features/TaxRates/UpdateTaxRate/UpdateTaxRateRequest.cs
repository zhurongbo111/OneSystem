using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.TaxRates.UpdateTaxRate;

/// <summary>
/// 编辑税率请求（全量覆盖语义：缺字段 / 空串一律清空，AGENTS.md §4.5；
/// 编码与名称均可改，唯一性由 Handler 校验）
/// </summary>
public sealed class UpdateTaxRateRequest : IRequest<TaxRateDetailDto>
{
    /// <summary>税率 id（取自路由，请求体缺省时由 Controller 覆盖）</summary>
    public Guid Id { get; init; }

    /// <summary>税率编码（全局唯一）</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>税率名称（全局唯一）</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>税率百分比数值（<c>13</c> 表示 13%）</summary>
    public decimal Rate { get; init; }

    /// <summary>税率状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; } = (int)TaxRateStatus.Enabled;

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }
}