namespace App.Core;

/// <summary>
/// 成本计算的通用无状态技术组件（口径正文见 specs/026-erp-cost/design.md §0.1）。
/// 各写入路径（采购 / 销售 / 盘点 / 退货及其作废）与成本重算共用本类的精度与舍入，
/// 避免各处自行实现导致口径分叉（舍入统一 4 位、AwayFromZero —— 财务惯例，非 .NET 默认银行家舍入）。
/// </summary>
public static class CostCalculator
{
    /// <summary>成本计算精度（存储与计算统一 4 位小数，展示层再收敛到 2 位）</summary>
    public const int Precision = 4;

    /// <summary>按成本口径四舍五入到 <see cref="Precision"/> 位</summary>
    public static decimal Round(decimal value) => Math.Round(value, Precision, MidpointRounding.AwayFromZero);

    /// <summary>
    /// 成本金额 = Round(数量 × 单价, 4)；数量为绝对值，符号由调用方按变动方向附加。
    /// </summary>
    public static decimal TotalCost(int quantity, decimal unitCost) => Round(quantity * unitCost);

    /// <summary>
    /// 移动加权平均单价 = Round(结存金额 ÷ 结存数量, 4)；数量为 0 时返回 0（均价由调用方决定保留逻辑）。
    /// </summary>
    public static decimal AverageCost(decimal costAmount, int quantity)
        => quantity == 0 ? 0m : Round(costAmount / quantity);
}
