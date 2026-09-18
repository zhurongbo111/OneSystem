using App.Core.Entities;

namespace App.Core.Features.StockMovements;

/// <summary>
/// 库存流水列表项出参（camelCase 与前端 DTO 一一对应）。
/// 后端只回原始枚举 + 带符号变动量；类型文案 / 颜色 / 符号展示由前端按
/// specs/019-erp-stock-movement/design.md §0 表渲染。
/// </summary>
public sealed record StockMovementListItemDto
{
    /// <summary>流水 ID</summary>
    public required string Id { get; init; }

    /// <summary>商品 ID</summary>
    public required string ProductId { get; init; }

    /// <summary>商品编码（联查 Products 带出）</summary>
    public required string ProductCode { get; init; }

    /// <summary>商品名称（联查 Products 带出）</summary>
    public required string ProductName { get; init; }

    /// <summary>计量单位（联查 Products 带出）</summary>
    public required string Unit { get; init; }

    /// <summary>变动类型（原始枚举，前端按 §0 表转文案 / 颜色）</summary>
    public required StockMovementType MovementType { get; init; }

    /// <summary>变动量（带符号：入库 / 回增为正，出库 / 回冲为负）</summary>
    public required int Quantity { get; init; }

    /// <summary>本次变动成本单价（erp-cost；numeric(18,4)）</summary>
    public decimal UnitCost { get; init; }

    /// <summary>本次变动成本金额（erp-cost；与 Quantity 同号，前端显示 2 位）</summary>
    public decimal TotalCost { get; init; }

    /// <summary>来源单据号（无来源单据时为 null，前端显示 -）</summary>
    public string? SourceNo { get; init; }

    /// <summary>备注（预留展示位，本期无写入来源）</summary>
    public string? Remark { get; init; }

    /// <summary>变动时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>操作人姓名（无操作人或无匹配用户时为 null，前端显示 -）</summary>
    public string? CreatedByName { get; init; }
}
