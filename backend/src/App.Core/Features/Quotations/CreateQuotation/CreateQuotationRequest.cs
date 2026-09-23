using App.Core.Abstractions;

namespace App.Core.Features.Quotations.CreateQuotation;

/// <summary>
/// 新增报价单请求（纯意向单：保存后不动库存、不写流水、不产生应收）。
/// 小计 / 总额不在此请求中——后端按 数量 × 单价 重算，不信任前端传值（design.md §0.2）。
/// </summary>
public sealed class CreateQuotationRequest : IRequest<QuotationDetailDto>
{
    /// <summary>客户 id</summary>
    public required Guid PartnerId { get; init; }

    /// <summary>报价日期（UTC 午夜，前端所选日期的 UTC 0 点转 ISO 串）</summary>
    public required DateTimeOffset QuotationDate { get; init; }

    /// <summary>报价有效期至，可空（不早于报价日期）</summary>
    public DateOnly? ValidUntil { get; init; }

    /// <summary>明细行（1–100 行；productId / quantity / unitPrice）</summary>
    public required IReadOnlyList<CreateQuotationItem> Items { get; init; }

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }
}

/// <summary>报价单明细行入参（名称 / 单位 / 单价快照由后端从商品档案与前端传入的单价落库）</summary>
public sealed class CreateQuotationItem
{
    /// <summary>商品 id</summary>
    public required Guid ProductId { get; init; }

    /// <summary>报价数量（≥ 1）</summary>
    public required int Quantity { get; init; }

    /// <summary>单价（≥ 0；取价规则见 design.md §0.2，后端按传入值落库、不二次取价）</summary>
    public required decimal UnitPrice { get; init; }
}
