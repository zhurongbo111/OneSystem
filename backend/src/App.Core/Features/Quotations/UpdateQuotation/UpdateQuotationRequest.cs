using App.Core.Abstractions;

namespace App.Core.Features.Quotations.UpdateQuotation;

/// <summary>
/// 编辑报价单请求（仅「草稿」状态可改，否则 40166；明细整体替换）。
/// 报价单号不可改（请求体不含），小计 / 总额由后端重算（design.md §3.4）。
/// </summary>
public sealed class UpdateQuotationRequest : IRequest<QuotationDetailDto>
{
    /// <summary>报价单 id（取自路由参数；请求体不含，缺失时反序列化为空值，控制器以路由 id 覆盖）</summary>
    public Guid Id { get; init; }

    /// <summary>客户 id</summary>
    public required Guid PartnerId { get; init; }

    /// <summary>报价日期（UTC 午夜）</summary>
    public required DateTimeOffset QuotationDate { get; init; }

    /// <summary>报价有效期至，可空（不早于报价日期）</summary>
    public DateOnly? ValidUntil { get; init; }

    /// <summary>明细行（1–100 行；整体替换）</summary>
    public required IReadOnlyList<UpdateQuotationItem> Items { get; init; }

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }
}

/// <summary>报价单明细行入参（编辑时整体替换）</summary>
public sealed class UpdateQuotationItem
{
    /// <summary>商品 id</summary>
    public required Guid ProductId { get; init; }

    /// <summary>报价数量（≥ 1）</summary>
    public required int Quantity { get; init; }

    /// <summary>单价（≥ 0）</summary>
    public required decimal UnitPrice { get; init; }
}
