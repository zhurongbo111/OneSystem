using App.Core.Abstractions;

namespace App.Core.Features.StockTakes.GetStockTakePickProducts;

/// <summary>
/// 盘点商品选择查询请求（只读，无格式字段）
/// </summary>
public sealed class GetStockTakePickProductsRequest : IRequest<IReadOnlyList<StockTakeProductPickDto>>
{
}
