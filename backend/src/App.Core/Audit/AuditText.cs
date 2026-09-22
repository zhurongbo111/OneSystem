using App.Core.Entities;

namespace App.Core.Audit;

/// <summary>
/// 审计专用枚举中文文案映射（集中一处，避免分散在各域造成的同义词问题。
/// 只服务审计日志——页面文案继续以各功能规格的映射表为准）。
/// </summary>
public static class AuditText
{
    /// <summary>往来单位类型文案</summary>
    /// <param name="type">单位类型</param>
    public static string PartnerType(PartnerType type) => type switch
    {
        Entities.PartnerType.Supplier => "供应商",
        Entities.PartnerType.Customer => "客户",
        Entities.PartnerType.Both => "两者",
        _ => AuditSummary.Empty,
    };

    /// <summary>往来单位状态文案</summary>
    /// <param name="status">状态</param>
    public static string PartnerStatus(PartnerStatus status)
        => status == Entities.PartnerStatus.Enabled ? "启用" : "停用";

    /// <summary>商品状态文案</summary>
    /// <param name="status">状态</param>
    public static string ProductStatus(ProductStatus status)
        => status == Entities.ProductStatus.Enabled ? "启用" : "停用";

    /// <summary>用户状态文案</summary>
    /// <param name="status">状态</param>
    public static string UserStatus(UserStatus status)
        => status == Entities.UserStatus.Enabled ? "启用" : "禁用";

    /// <summary>单据作废状态文案（OrderStatus）</summary>
    /// <param name="status">状态</param>
    public static string OrderStatus(OrderStatus status)
        => status == Entities.OrderStatus.Normal ? "正常" : "已作废";

    /// <summary>订单流转状态文案；采购 / 销售侧称谓不同时由 <paramref name="isPurchase"/> 区分</summary>
    /// <param name="status">流转状态</param>
    /// <param name="isPurchase">是否采购侧（销售侧「收货」作「发货」）</param>
    public static string OrderFlowStatus(OrderFlowStatus status, bool isPurchase = true) => status switch
    {
        Entities.OrderFlowStatus.Voided => "已作废",
        Entities.OrderFlowStatus.Pending => isPurchase ? "待收货" : "待发货",
        Entities.OrderFlowStatus.Partial => isPurchase ? "部分收货" : "部分发货",
        Entities.OrderFlowStatus.Completed => isPurchase ? "已完成收货" : "已完成发货",
        Entities.OrderFlowStatus.Closed => "已关闭",
        _ => AuditSummary.Empty,
    };

    /// <summary>收付款方式文案</summary>
    /// <param name="method">结算方式</param>
    public static string SettlementMethod(SettlementMethod method) => method switch
    {
        Entities.SettlementMethod.Cash => "现金",
        Entities.SettlementMethod.BankTransfer => "银行转账",
        Entities.SettlementMethod.Other => "其他",
        _ => AuditSummary.Empty,
    };

    /// <summary>收付款方向文案</summary>
    /// <param name="type">收付类型</param>
    public static string SettlementType(SettlementType type)
        => type == Entities.SettlementType.Receipt ? "收款" : "付款";

    /// <summary>被核销单据类型文案</summary>
    /// <param name="type">被核销单据类型</param>
    public static string SettlementOrderType(SettlementOrderType type) => type switch
    {
        Entities.SettlementOrderType.PurchaseInbound => "采购入库单",
        Entities.SettlementOrderType.SalesOutbound => "销售出库单",
        Entities.SettlementOrderType.PurchaseReturn => "采购退货单",
        Entities.SettlementOrderType.SalesReturn => "销售退货单",
        _ => AuditSummary.Empty,
    };

    /// <summary>盘点单据类型文案</summary>
    /// <param name="type">盘点类型</param>
    public static string StockTakeType(StockTakeType type)
        => type == Entities.StockTakeType.Initial ? "期初建账" : "库存盘点";

    /// <summary>单据结算状态文案</summary>
    /// <param name="state">结算状态</param>
    public static string SettlementState(SettlementState state) => state switch
    {
        Entities.SettlementState.Unsettled => "未结算",
        Entities.SettlementState.PartiallySettled => "部分结算",
        Entities.SettlementState.Settled => "已结算",
        _ => AuditSummary.Empty,
    };

    /// <summary>部门状态文案</summary>
    /// <param name="status">状态</param>
    public static string DepartmentStatus(DepartmentStatus status)
        => status == Entities.DepartmentStatus.Enabled ? "启用" : "停用";

    /// <summary>岗位状态文案</summary>
    /// <param name="status">状态</param>
    public static string PositionStatus(PositionStatus status)
        => status == Entities.PositionStatus.Enabled ? "启用" : "停用";

    /// <summary>员工在职状态文案</summary>
    /// <param name="status">状态</param>
    public static string EmployeeStatus(EmployeeStatus status)
        => status == Entities.EmployeeStatus.Active ? "在职" : "离职";

    /// <summary>性别文案（未填输出空值占位）</summary>
    /// <param name="gender">性别，可空</param>
    public static string Gender(Gender? gender) => gender switch
    {
        Entities.Gender.Male => "男",
        Entities.Gender.Female => "女",
        Entities.Gender.Unknown => AuditSummary.Empty,
        _ => AuditSummary.Empty,
    };
}
