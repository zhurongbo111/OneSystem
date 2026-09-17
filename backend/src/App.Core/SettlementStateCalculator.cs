using App.Core.Entities;

namespace App.Core;

/// <summary>
/// 单据结算状态推导（specs/023-erp-settlement/design.md §0）：结算状态与未结金额均由
/// 已结算金额与总额推导，不落列；四类单据的 DTO 映射共用本类，避免各 Mapper 重复实现。
/// </summary>
public static class SettlementStateCalculator
{
    /// <summary>
    /// 按总额与已结算金额推导展示用结算状态：
    /// 0 未结算（≤ 0）/ 1 部分结算（0 &lt; x &lt; 总额）/ 2 已结算（≥ 总额）。
    /// </summary>
    /// <param name="totalAmount">单据总额</param>
    /// <param name="settledAmount">已结算金额</param>
    public static SettlementState Derive(decimal totalAmount, decimal settledAmount)
    {
        if (settledAmount <= 0m)
        {
            return SettlementState.Unsettled;
        }

        return settledAmount >= totalAmount ? SettlementState.Settled : SettlementState.PartiallySettled;
    }

    /// <summary>
    /// 未结金额 = 总额 − 已结算金额（推导，不落列）。
    /// 核销超额由创建用例拦截（40112），正常数据下不会为负。
    /// </summary>
    /// <param name="totalAmount">单据总额</param>
    /// <param name="settledAmount">已结算金额</param>
    public static decimal UnsettledAmount(decimal totalAmount, decimal settledAmount)
        => totalAmount - settledAmount;
}
