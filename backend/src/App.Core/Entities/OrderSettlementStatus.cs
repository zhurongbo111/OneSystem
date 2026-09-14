namespace App.Core.Entities;

/// <summary>
/// 单据结算状态（采购单 / 销售单共用同一枚举，语义按方向区分：采购 = 未付 / 已付，销售 = 未收 / 已收）。
/// </summary>
public enum OrderSettlementStatus
{
    /// <summary>未结算（采购 = 未付，销售 = 未收）</summary>
    Unsettled = 0,

    /// <summary>已结算（采购 = 已付，销售 = 已收）</summary>
    Settled = 1,
}
