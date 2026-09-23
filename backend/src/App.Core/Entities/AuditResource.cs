namespace App.Core.Entities;

/// <summary>
/// 审计资源类型枚举（<c>specs/029-erp-audit-log/design.md</c> §0.1 范围表的资源列）。
/// 取值即存储值，新增资源在本枚举续行并在 §0.1 登记；前端下拉文案另行映射（后端只回枚举值）。
/// </summary>
public enum AuditResource
{
    /// <summary>商品</summary>
    Product = 0,

    /// <summary>商品分类</summary>
    Category = 1,

    /// <summary>往来单位</summary>
    Partner = 2,

    /// <summary>仓库（038）</summary>
    Warehouse = 3,

    /// <summary>客户价格（036）</summary>
    PartnerPrice = 4,

    /// <summary>用户</summary>
    User = 5,

    /// <summary>角色</summary>
    Role = 6,

    /// <summary>采购入库单</summary>
    PurchaseReceipt = 7,

    /// <summary>销售出库单</summary>
    SalesShipment = 8,

    /// <summary>采购退货单</summary>
    PurchaseReturn = 9,

    /// <summary>销售退货单</summary>
    SalesReturn = 10,

    /// <summary>收付款单</summary>
    Settlement = 11,

    /// <summary>库存盘点单</summary>
    StockTake = 12,

    /// <summary>调拨单（039）</summary>
    Transfer = 13,

    /// <summary>发票（032）</summary>
    Invoice = 14,

    /// <summary>成本重算</summary>
    Cost = 15,

    /// <summary>单据审批（042）</summary>
    Approval = 16,

    /// <summary>采购订单（024）</summary>
    PurchaseOrder = 17,

    /// <summary>销售订单（024）</summary>
    SalesOrder = 18,

    /// <summary>部门（030）</summary>
    Department = 19,

    /// <summary>岗位（030）</summary>
    Position = 20,

    /// <summary>员工（030）</summary>
    Employee = 21,

    /// <summary>会计科目（031）</summary>
    Account = 22,

    /// <summary>税率（031）</summary>
    TaxRate = 23,

    /// <summary>会计期间（033）</summary>
    AccountingPeriod = 24,

    /// <summary>记账凭证（033）</summary>
    Voucher = 25,

    /// <summary>科目映射（033）</summary>
    AccountMapping = 26,

    /// <summary>资金账户（034）</summary>
    BankAccount = 27,
}
