namespace App.Core.Entities;

/// <summary>
/// 单据状态（采购单 / 销售单共用：作废 / 正常）。
/// 单据不可编辑，作废后禁止再操作；作废单号不复用。
/// </summary>
public enum OrderStatus
{
    /// <summary>已作废</summary>
    Voided = 0,

    /// <summary>正常</summary>
    Normal = 1,
}
