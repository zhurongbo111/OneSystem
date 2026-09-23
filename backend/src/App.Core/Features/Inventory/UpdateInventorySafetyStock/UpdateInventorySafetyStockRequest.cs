using App.Core.Abstractions;

namespace App.Core.Features.Inventory.UpdateInventorySafetyStock;

/// <summary>
/// 维护仓级安全库存请求（038）：库存查询页行内「安全库存」单字段 Modal 提交
/// </summary>
public sealed class UpdateInventorySafetyStockRequest : IRequest<UpdateInventorySafetyStockResponse>
{
    /// <summary>商品 id</summary>
    public Guid ProductId { get; init; }

    /// <summary>仓库 id</summary>
    public Guid WarehouseId { get; init; }

    /// <summary>安全库存阈值（0 表示不提醒）</summary>
    public int SafetyStock { get; init; }
}
