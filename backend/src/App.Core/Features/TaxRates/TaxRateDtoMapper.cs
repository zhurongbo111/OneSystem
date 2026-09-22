using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.TaxRates;

/// <summary>
/// 税率出参映射（集中一处，避免各用例重复拼装）
/// </summary>
internal static class TaxRateDtoMapper
{
    /// <summary>税率列表读模型 → 列表项出参</summary>
    public static TaxRateListItemDto ToTaxRateListItemDto(TaxRateListItem item)
        => new()
        {
            Id = item.Id.ToString(),
            Code = item.Code,
            Name = item.Name,
            Rate = item.Rate,
            Status = (int)item.Status,
            Remark = item.Remark,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
        };

    /// <summary>税率实体 → 税率详情出参</summary>
    public static TaxRateDetailDto ToTaxRateDetailDto(TaxRate taxRate)
        => new()
        {
            Id = taxRate.Id.ToString(),
            Code = taxRate.Code,
            Name = taxRate.Name,
            Rate = taxRate.Rate,
            Status = (int)taxRate.Status,
            Remark = taxRate.Remark,
            CreatedAt = taxRate.CreatedAt,
            UpdatedAt = taxRate.UpdatedAt,
        };
}