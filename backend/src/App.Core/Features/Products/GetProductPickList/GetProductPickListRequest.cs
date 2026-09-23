using App.Core.Abstractions;

namespace App.Core.Features.Products.GetProductPickList;

/// <summary>
/// 开单商品选择请求（不定义 Validator）
/// </summary>
public sealed class GetProductPickListRequest : IRequest<IReadOnlyList<ProductPickDto>>
{
    /// <summary>仓库 id，可空（038：传仓则「当前库存」为该仓数量，不传为各仓合计）</summary>
    public Guid? WarehouseId { get; init; }
}
