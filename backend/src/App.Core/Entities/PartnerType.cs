namespace App.Core.Entities;

/// <summary>
/// 往来单位类型（供应商 / 客户 / 两者；采购单校验 Type ∈ {Supplier, Both}，销售单校验 Type ∈ {Customer, Both}）
/// </summary>
public enum PartnerType
{
    /// <summary>供应商</summary>
    Supplier = 1,

    /// <summary>客户</summary>
    Customer = 2,

    /// <summary>两者（一份档案同时用于采购与销售）</summary>
    Both = 3,
}
