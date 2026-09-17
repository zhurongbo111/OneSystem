using App.Core.Abstractions;

namespace App.Core.Features.StockTakes.GetStockTakeById;

/// <summary>
/// 盘点单详情查询请求（只读；纯参数用例，无格式校验器）
/// </summary>
public sealed class GetStockTakeByIdRequest : IRequest<StockTakeDetailDto>
{
    /// <summary>盘点单 id</summary>
    public required Guid Id { get; init; }
}
