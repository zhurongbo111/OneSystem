using App.Core.Entities;

namespace App.Core.Exports;

/// <summary>
/// 导出（Excel）单元格的枚举文案：与列表页展示文案一致（如「正常 / 已作废」），
/// 避免在 xlsx 里出现 0 / 1 这类裸枚举值。仅服务于导出出参；
/// 页面展示文案由前端各自维护（本类不参与接口 JSON 输出）。
/// </summary>
public static class ExportLabels
{
    /// <summary>商品状态：启用 / 停用</summary>
    public static string ToText(ProductStatus status)
        => status == ProductStatus.Enabled ? "启用" : "停用";

    /// <summary>往来单位状态：启用 / 停用</summary>
    public static string ToText(PartnerStatus status)
        => status == PartnerStatus.Enabled ? "启用" : "停用";

    /// <summary>往来单位类型：供应商 / 客户 / 两者</summary>
    public static string ToText(PartnerType type)
        => type switch
        {
            PartnerType.Supplier => "供应商",
            PartnerType.Customer => "客户",
            _ => "两者",
        };

    /// <summary>单据状态：正常 / 已作废</summary>
    public static string ToText(OrderStatus status)
        => status == OrderStatus.Normal ? "正常" : "已作废";

    /// <summary>员工在职状态：在职 / 离职</summary>
    public static string ToText(EmployeeStatus status)
        => status == EmployeeStatus.Active ? "在职" : "离职";

    /// <summary>性别：男 / 女（未填输出占位符）</summary>
    public static string ToText(Gender? gender) => gender switch
    {
        Entities.Gender.Male => "男",
        Entities.Gender.Female => "女",
        _ => OrDash(null),
    };

    /// <summary>库存变动类型文案（specs/019-erp-stock-movement/design.md §0）</summary>
    public static string ToText(StockMovementType type)
        => type switch
        {
            StockMovementType.PurchaseInbound => "采购入库",
            StockMovementType.PurchaseVoid => "采购作废",
            StockMovementType.SalesOutbound => "销售出库",
            StockMovementType.SalesVoid => "销售作废",
            StockMovementType.InitialStock => "期初建账",
            StockMovementType.StockTakeAdjust => "盘点调整",
            StockMovementType.PurchaseReturnOut => "采购退货",
            StockMovementType.PurchaseReturnVoid => "采购退货作废",
            StockMovementType.SalesReturnIn => "销售退货",
            _ => "销售退货作废",
        };

    /// <summary>盘点单据类型：期初建账 / 库存盘点</summary>
    public static string ToText(StockTakeType type)
        => type == StockTakeType.Initial ? "期初建账" : "库存盘点";

    /// <summary>收付款类型：收款 / 付款</summary>
    public static string ToText(SettlementType type)
        => type == SettlementType.Receipt ? "收款" : "付款";

    /// <summary>收付款方式：现金 / 银行转账 / 其他</summary>
    public static string ToText(SettlementMethod method)
        => method switch
        {
            SettlementMethod.Cash => "现金",
            SettlementMethod.BankTransfer => "银行转账",
            _ => "其他",
        };

    /// <summary>
    /// 被核销单据类型：采购入库单 / 销售出库单 / 采购退货单 / 销售退货单；
    /// 与收付款详情页口径一致（前端 SettlementDetailView.vue）。
    /// </summary>
    public static string ToText(SettlementOrderType orderType)
        => orderType switch
        {
            SettlementOrderType.PurchaseInbound => "采购入库单",
            SettlementOrderType.SalesOutbound => "销售出库单",
            SettlementOrderType.PurchaseReturn => "采购退货单",
            _ => "销售退货单",
        };

    /// <summary>
    /// 发票类型：进项 / 销项（specs/032-erp-invoice/design.md §0.4）
    /// </summary>
    public static string ToText(InvoiceType type)
        => type == InvoiceType.Purchase ? "进项" : "销项";

    /// <summary>
    /// 税率百分比文案：入参为 0–1 小数口径（如 <c>0.13</c>），输出「13%」；
    /// 由发票导出使用（页面展示同口径，specs/032-erp-invoice/design.md §4.2）。
    /// </summary>
    /// <param name="taxRate">税率（0–1 小数口径）</param>
    public static string ToPercentage(decimal taxRate)
        => $"{taxRate * 100:0.####}%";

    /// <summary>
    /// 空值单元格的统一占位文案（<c>-</c>）：`null` / 空串 / 纯空白一律输出占位符，
    /// 避免 xlsx 里出现无法辨识的空白单元格（与页面对空值的展示口径一致）。
    /// </summary>
    /// <param name="value">原始文本值</param>
    public static string OrDash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value;

    /// <summary>
    /// 结算状态文案：未结算 / 部分结算（未结 X.XX）/ 已结算；与列表页口径一致（前端 utils/settlement.ts）。
    /// </summary>
    /// <param name="state">结算状态（由已结金额与总额推导）</param>
    /// <param name="unsettledAmount">未结金额（仅部分结算时展示）</param>
    public static string ToText(SettlementState state, decimal unsettledAmount)
        => state switch
        {
            SettlementState.PartiallySettled => $"部分结算（未结 {unsettledAmount:0.00}）",
            SettlementState.Settled => "已结算",
            _ => "未结算",
        };
}
