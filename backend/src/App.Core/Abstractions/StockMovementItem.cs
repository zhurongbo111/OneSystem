using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 库存流水列表项读模型（联查 Products / Users 带出，不暴露实体）。
/// 商品编码 / 名称 / 单位与操作人姓名为联查快照，无匹配用户或操作人为空时 CreatedByName 为 null。
/// </summary>
public sealed record StockMovementItem
{
    /// <summary>流水 ID</summary>
    public required Guid Id { get; init; }

    /// <summary>商品 ID</summary>
    public required Guid ProductId { get; init; }

    /// <summary>商品编码（联查 Products 带出）</summary>
    public required string ProductCode { get; init; }

    /// <summary>商品名称（联查 Products 带出）</summary>
    public required string ProductName { get; init; }

    /// <summary>计量单位（联查 Products 带出）</summary>
    public required string Unit { get; init; }

    /// <summary>仓库 ID（038）</summary>
    public required Guid WarehouseId { get; init; }

    /// <summary>仓库名称（联查 Warehouses 带出；仓改名后历史流水显示当时名称由名称列决定，本项目按当前名称展示）</summary>
    public required string WarehouseName { get; init; }

    /// <summary>变动类型</summary>
    public required StockMovementType MovementType { get; init; }

    /// <summary>变动量（带符号：入库 / 回增为正，出库 / 回冲为负）</summary>
    public required int Quantity { get; init; }

    /// <summary>本次变动成本单价（erp-cost；numeric(18,4)）</summary>
    public required decimal UnitCost { get; init; }

    /// <summary>本次变动成本金额（erp-cost；numeric(18,4)，与 Quantity 同号）</summary>
    public required decimal TotalCost { get; init; }

    /// <summary>来源单据号（无来源单据时为 null）</summary>
    public string? SourceNo { get; init; }

    /// <summary>备注（预留展示位，本期无写入来源）</summary>
    public string? Remark { get; init; }

    /// <summary>变动时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>操作人姓名（Users.DisplayName 联查带出；CreatedBy 为空或无匹配用户时为 null）</summary>
    public string? CreatedByName { get; init; }
}
