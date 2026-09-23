using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.PartnerPrices;

/// <summary>
/// 客户价格出参映射（正向：读模型 / 实体 → DTO；方法名统一为 To + 目标 DTO 类型名）。
/// </summary>
internal static class PartnerPricesDtoMapper
{
    /// <summary>
    /// 协议价实体 + 客户名与商品（联查来源）→ 详情出参
    /// </summary>
    public static PartnerPriceDetailDto ToPartnerPriceDetailDto(PartnerPrice entity, string partnerName, Product product)
        => new()
        {
            Id = entity.Id.ToString(),
            PartnerId = entity.PartnerId.ToString(),
            PartnerName = partnerName,
            ProductId = entity.ProductId.ToString(),
            ProductCode = product.Code,
            ProductName = product.Name,
            Unit = product.Unit,
            Price = entity.Price,
            SalePrice = product.SalePrice,
            Remark = entity.Remark,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    /// <summary>
    /// 列表读模型 → 列表出参
    /// </summary>
    public static PartnerPriceListItemDto ToPartnerPriceListItemDto(PartnerPriceListItem item)
        => new()
        {
            Id = item.Id.ToString(),
            PartnerId = item.PartnerId.ToString(),
            PartnerName = item.PartnerName,
            ProductId = item.ProductId.ToString(),
            ProductCode = item.ProductCode,
            ProductName = item.ProductName,
            Unit = item.Unit,
            Price = item.Price,
            SalePrice = item.SalePrice,
            Remark = item.Remark,
            CreatedAt = item.CreatedAt,
        };

    /// <summary>
    /// 生效价读模型 → 批量取价出参
    /// </summary>
    public static EffectivePriceDto ToEffectivePriceDto(EffectivePriceItem item)
        => new()
        {
            ProductId = item.ProductId.ToString(),
            UnitPrice = item.UnitPrice,
            Source = (int)item.Source,
        };
}