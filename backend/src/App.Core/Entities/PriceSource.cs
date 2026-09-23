namespace App.Core.Entities;

/// <summary>
/// 取价来源（批量取价输出的单价来源，用于前端「协议价 / 默认价」标注；
/// 优先级见 specs/036-erp-partner-price/design.md §0.1）。
/// </summary>
public enum PriceSource
{
    /// <summary>客户协议价（该客户 × 商品已配置协议价）</summary>
    Agreement = 0,

    /// <summary>商品销售价（未配置协议价时的兜底）</summary>
    Default = 1,
}