namespace App.Core.Features.Reports;

/// <summary>
/// 销售汇总行出参（净额 = 出库 − 退货，由 Mapper 计算；与采购汇总同构）。
/// </summary>
public sealed class SalesSummaryItemDto
{
    /// <summary>分组键（客户 id 或商品 id；理论不为空，保留可空以容忍历史脏数据）</summary>
    public string? Key { get; init; }

    /// <summary>分组名称（客户名称或商品名称）</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>计量单位（仅商品维度有值）</summary>
    public string? Unit { get; init; }

    /// <summary>出库单数</summary>
    public int OrderCount { get; init; }

    /// <summary>出库数量</summary>
    public int OutboundQuantity { get; init; }

    /// <summary>出库金额</summary>
    public decimal OutboundAmount { get; init; }

    /// <summary>退货数量</summary>
    public int ReturnQuantity { get; init; }

    /// <summary>退货金额</summary>
    public decimal ReturnAmount { get; init; }

    /// <summary>净数量 = 出库数量 − 退货数量</summary>
    public int NetQuantity { get; init; }

    /// <summary>净额 = 出库金额 − 退货金额</summary>
    public decimal NetAmount { get; init; }
}

/// <summary>
/// 销售汇总合计出参（全量筛选结果口径；净额由 Mapper 计算）。
/// </summary>
public sealed class SalesSummaryTotalDto
{
    /// <summary>出库单数合计</summary>
    public int OrderCount { get; init; }

    /// <summary>出库数量合计</summary>
    public int OutboundQuantity { get; init; }

    /// <summary>出库金额合计</summary>
    public decimal OutboundAmount { get; init; }

    /// <summary>退货数量合计</summary>
    public int ReturnQuantity { get; init; }

    /// <summary>退货金额合计</summary>
    public decimal ReturnAmount { get; init; }

    /// <summary>净数量合计</summary>
    public int NetQuantity { get; init; }

    /// <summary>净额合计</summary>
    public decimal NetAmount { get; init; }
}
