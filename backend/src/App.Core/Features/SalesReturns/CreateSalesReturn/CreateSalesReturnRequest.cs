using App.Core.Abstractions;

namespace App.Core.Features.SalesReturns.CreateSalesReturn;

/// <summary>
/// 新增销售退货单请求（一步式：保存即生效，库存立即回增）。
/// 小计 / 总额不在此请求中——后端按 数量 × 单价 重算，不信任前端传值。
/// </summary>
public sealed class CreateSalesReturnRequest : IRequest<SalesReturnDetailDto>
{
    /// <summary>客户 id</summary>
    public required Guid PartnerId { get; init; }

    /// <summary>业务日期（UTC 午夜，前端所选日期的本地 0 点转 UTC ISO 串）</summary>
    public required DateTimeOffset ReturnDate { get; init; }

    /// <summary>入库仓 id，可空（038；不传 = 默认仓，兼容存量调用方）</summary>
    public Guid? WarehouseId { get; init; }

    /// <summary>明细行（1–100 行；productId / quantity / unitPrice）</summary>
    public required IReadOnlyList<CreateSalesReturnItem> Items { get; init; }

    /// <summary>备注（可写原销售单号 / 退货原因），可空</summary>
    public string? Remark { get; init; }
}

/// <summary>销售退货单明细行入参（快照字段由后端从商品档案带出）</summary>
public sealed class CreateSalesReturnItem
{
    /// <summary>商品 id</summary>
    public required Guid ProductId { get; init; }

    /// <summary>退货数量（≥ 1）</summary>
    public required int Quantity { get; init; }

    /// <summary>单价（≥ 0，默认带出商品销售价、开单时可改）</summary>
    public required decimal UnitPrice { get; init; }
}
