namespace App.Core.Features.Reports;

/// <summary>
/// 进销存报表行出参（对应读模型 <c>InventoryFlowItem</c>；金额与时间不涉及，数量均为整数）。
/// </summary>
public sealed class InventoryFlowItemDto
{
    /// <summary>商品 ID</summary>
    public string ProductId { get; init; } = string.Empty;

    /// <summary>商品编码</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>商品名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>分类名称</summary>
    public string CategoryName { get; init; } = string.Empty;

    /// <summary>计量单位</summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>期初数量</summary>
    public int OpeningQuantity { get; init; }

    /// <summary>期间入</summary>
    public int InboundQuantity { get; init; }

    /// <summary>期间出</summary>
    public int OutboundQuantity { get; init; }

    /// <summary>期末数量（期初 + 期间入 − 期间出）</summary>
    public int ClosingQuantity { get; init; }
}

/// <summary>
/// 进销存报表合计出参（全量筛选结果口径）。
/// </summary>
public sealed class InventoryFlowSummaryDto
{
    /// <summary>期初数量合计</summary>
    public int OpeningQuantity { get; init; }

    /// <summary>期间入合计</summary>
    public int InboundQuantity { get; init; }

    /// <summary>期间出合计</summary>
    public int OutboundQuantity { get; init; }

    /// <summary>期末数量合计</summary>
    public int ClosingQuantity { get; init; }
}
