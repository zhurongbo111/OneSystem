namespace App.Core.Entities;

/// <summary>
/// 订单流转状态（采购订单 / 销售订单共用）。
/// 文案与颜色映射见 specs/024-erp-order-flow design.md §0（采购侧「待收货」、销售侧「待发货」）。
/// 由明细累计量与显式操作（作废 / 关闭）共同决定，不引入第二个状态列。
/// </summary>
public enum OrderFlowStatus
{
    /// <summary>已作废（未开始收货时可作废；终态）</summary>
    Voided = 0,

    /// <summary>待收货（采购）/ 待发货（销售）：尚未发生任何出入库</summary>
    Pending = 1,

    /// <summary>部分收货（采购）/ 部分发货（销售）：已有出入库但未执行完</summary>
    Partial = 2,

    /// <summary>已完成：全部明细的累计执行量 = 订购数量</summary>
    Completed = 3,

    /// <summary>已关闭：人工决定剩余不再执行（保留累计量，不再接受关联出入库）</summary>
    Closed = 4,
}
