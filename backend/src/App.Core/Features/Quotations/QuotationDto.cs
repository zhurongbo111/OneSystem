namespace App.Core.Features.Quotations;

/// <summary>
/// 报价单出参共享模型（主表 + 明细，与前端 DTO camelCase 一一对应；列表行用 QuotationListItemDto）。
/// 状态文案与颜色映射见 specs/037-erp-quotation design.md §0.1（草稿 / 已转订单 / 已作废）。
/// </summary>
public sealed class QuotationDetailDto
{
    /// <summary>报价单 id</summary>
    public required string Id { get; init; }

    /// <summary>报价单号</summary>
    public required string QuotationNo { get; init; }

    /// <summary>客户 id</summary>
    public required string PartnerId { get; init; }

    /// <summary>客户名称快照</summary>
    public required string PartnerName { get; init; }

    /// <summary>报价日期</summary>
    public required DateTimeOffset QuotationDate { get; init; }

    /// <summary>报价有效期至，可空（过期仅前端展示「已过期」，状态不变）</summary>
    public DateOnly? ValidUntil { get; init; }

    /// <summary>总金额（后端重算值）</summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>报价单状态（0 草稿 / 1 已转订单 / 2 已作废）</summary>
    public required int Status { get; init; }

    /// <summary>转出的销售订单 id，可空</summary>
    public string? ConvertedOrderId { get; init; }

    /// <summary>转出的销售订单号快照，可空</summary>
    public string? ConvertedOrderNo { get; init; }

    /// <summary>备注</summary>
    public string? Remark { get; init; }

    /// <summary>创建人用户 id</summary>
    public string? CreatedBy { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>明细行（按插入顺序）</summary>
    public required IReadOnlyList<QuotationItemDto> Items { get; init; }
}

/// <summary>报价单明细出参模型</summary>
public sealed class QuotationItemDto
{
    /// <summary>明细行 id</summary>
    public required string Id { get; init; }

    /// <summary>商品 id</summary>
    public required string ProductId { get; init; }

    /// <summary>商品名称快照</summary>
    public required string ProductName { get; init; }

    /// <summary>计量单位快照</summary>
    public required string Unit { get; init; }

    /// <summary>报价数量</summary>
    public required int Quantity { get; init; }

    /// <summary>单价快照</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>小计（后端重算值）</summary>
    public required decimal Subtotal { get; init; }
}

/// <summary>报价单列表行出参模型（含明细行数）</summary>
public sealed class QuotationListItemDto
{
    /// <summary>报价单 id</summary>
    public required string Id { get; init; }

    /// <summary>报价单号</summary>
    public required string QuotationNo { get; init; }

    /// <summary>客户 id</summary>
    public required string PartnerId { get; init; }

    /// <summary>客户名称快照</summary>
    public required string PartnerName { get; init; }

    /// <summary>报价日期</summary>
    public required DateTimeOffset QuotationDate { get; init; }

    /// <summary>报价有效期至，可空</summary>
    public DateOnly? ValidUntil { get; init; }

    /// <summary>总金额</summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>明细行数</summary>
    public required int ItemCount { get; init; }

    /// <summary>报价单状态（0 草稿 / 1 已转订单 / 2 已作废）</summary>
    public required int Status { get; init; }

    /// <summary>转出的销售订单号快照，可空</summary>
    public string? ConvertedOrderNo { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// 报价单转销售订单结果出参（design.md §3.3：`{ orderId, orderNo }`）：
/// 前端据此跳转销售订单详情，并提示报价单已锁定为「已转订单」。
/// </summary>
public sealed class ConvertQuotationResultDto
{
    /// <summary>生成的销售订单 id</summary>
    public required string OrderId { get; init; }

    /// <summary>生成的销售订单号</summary>
    public required string OrderNo { get; init; }
}
