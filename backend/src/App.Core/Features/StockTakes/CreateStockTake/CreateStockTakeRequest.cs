using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.StockTakes.CreateStockTake;

/// <summary>
/// 新增盘点 / 期初建账单请求（一步式：保存即生效，库存按实盘数量设定 + 差异行写流水）。
/// 账面数量与差异不在此请求中——后端在事务内读取账面并重算 Difference，不信任前端传值（见 design.md §1）。
/// </summary>
public sealed class CreateStockTakeRequest : IRequest<StockTakeDetailDto>
{
    /// <summary>单据类型（0 期初建账 / 1 库存盘点）</summary>
    public required StockTakeType Type { get; init; }

    /// <summary>盘点业务日期（UTC 午夜，前端所选日期的本地 0 点转 UTC ISO 串）</summary>
    public required DateTimeOffset TakeDate { get; init; }

    /// <summary>明细行（1–100 行；productId / actualQuantity）</summary>
    public required IReadOnlyList<CreateStockTakeItem> Items { get; init; }

    /// <summary>备注（盘点说明 / 差异原因），可空</summary>
    public string? Remark { get; init; }
}

/// <summary>盘点明细行入参（编码 / 名称 / 单位 / 账面由后端快照，差异由后端重算）</summary>
public sealed class CreateStockTakeItem
{
    /// <summary>商品 id</summary>
    public required Guid ProductId { get; init; }

    /// <summary>实盘数量（≥ 0）</summary>
    public required int ActualQuantity { get; init; }

    /// <summary>
    /// 期初成本单价（erp-cost）：**期初建账必填**（0 ~ 9999999.99，库存成本的基线，缺价会导致后续均价失真）；
    /// 库存盘点必须为空（按当时移动加权均价处理，传入即 <c>40000</c>，避免误传成本）。
    /// </summary>
    public decimal? UnitCost { get; init; }
}
