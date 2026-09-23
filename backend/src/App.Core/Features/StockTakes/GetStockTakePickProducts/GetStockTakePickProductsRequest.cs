using App.Core.Abstractions;

namespace App.Core.Features.StockTakes.GetStockTakePickProducts;

/// <summary>
/// 盘点商品选择查询请求（只读）：按所选仓带出账面数量与「该仓是否已发生库存变动」（038）
/// </summary>
public sealed class GetStockTakePickProductsRequest : IRequest<IReadOnlyList<StockTakeProductPickDto>>
{
    /// <summary>盘点仓 id，可空（038；不传 = 默认仓）</summary>
    public Guid? WarehouseId { get; init; }
}
