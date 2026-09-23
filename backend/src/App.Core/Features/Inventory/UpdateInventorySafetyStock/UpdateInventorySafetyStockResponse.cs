namespace App.Core.Features.Inventory.UpdateInventorySafetyStock;

/// <summary>
/// 维护仓级安全库存出参（返回保存后的行状态，前端据此就地更新列表行）
/// </summary>
public sealed record UpdateInventorySafetyStockResponse
{
    /// <summary>商品 ID</summary>
    public required string ProductId { get; init; }

    /// <summary>仓库 ID</summary>
    public required string WarehouseId { get; init; }

    /// <summary>保存后的安全库存阈值</summary>
    public required int SafetyStock { get; init; }

    /// <summary>该仓当前库存</summary>
    public required int StockQuantity { get; init; }

    /// <summary>是否低于安全库存（SafetyStock &gt; 0 且 Stock &lt; SafetyStock）</summary>
    public required bool IsBelowSafetyStock { get; init; }
}
