namespace App.Core.Errors;

/// <summary>
/// 全局错误码定义，与 AGENTS.md 第 4.2 节一致；业务错误码扩展在各功能 design.md 中定义
/// </summary>
public static class ErrorCode
{
    /// <summary>成功</summary>
    public const int Success = 0;

    /// <summary>参数错误</summary>
    public const int Validation = 40000;

    /// <summary>未登录或 token 无效</summary>
    public const int Unauthorized = 40100;

    /// <summary>无权限</summary>
    public const int Forbidden = 40300;

    /// <summary>资源不存在</summary>
    public const int NotFound = 40400;

    /// <summary>服务内部错误</summary>
    public const int Internal = 50000;

    /// <summary>用户名或密码错误（project-scaffold 功能业务码）</summary>
    public const int LoginFailed = 40001;

    /// <summary>用户名已存在（user-management 功能业务码）</summary>
    public const int UsernameExists = 40002;

    /// <summary>邮箱已被使用</summary>
    public const int EmailExists = 40003;

    /// <summary>手机号已被使用</summary>
    public const int PhoneExists = 40004;

    /// <summary>账号已被禁用（登录时）</summary>
    public const int UserDisabled = 40005;

    /// <summary>不能禁用当前登录账号</summary>
    public const int CannotDisableSelf = 40006;

    /// <summary>商品编码已存在（erp-product 功能业务码）</summary>
    public const int ProductCodeExists = 40101;

    /// <summary>往来单位名称已存在（erp-partner 功能业务码）</summary>
    public const int PartnerNameExists = 40102;

    /// <summary>商品分类名称已存在（erp-product 功能业务码）</summary>
    public const int CategoryNameExists = 40105;

    /// <summary>分类已被商品引用，禁止删除（erp-product 功能业务码）</summary>
    public const int CategoryInUse = 40106;

    /// <summary>库存不足（erp-sale 功能业务码；message 含首个不足商品名，整单拒绝回滚）</summary>
    public const int InsufficientStock = 40103;

    /// <summary>商品已停用，不可用于开单（erp-product 预留，供 erp-purchase / erp-sale 使用）</summary>
    public const int ProductDisabled = 40107;

    /// <summary>单据已作废，禁止再操作（erp-purchase / erp-sale 共用；作废为终态）</summary>
    public const int OrderVoided = 40104;

    /// <summary>往来单位已停用，不可用于开单（erp-purchase / erp-sale 共用）</summary>
    public const int PartnerDisabled = 40108;

    /// <summary>往来单位类型与单据不匹配（如拿客户开采购单；erp-purchase / erp-sale 共用）</summary>
    public const int PartnerTypeMismatch = 40109;

    /// <summary>单据明细不能为空（erp-purchase / erp-sale 共用；明细行数 ≥ 1）</summary>
    public const int OrderItemsEmpty = 40110;

    /// <summary>期初建账只允许从未发生库存变动的商品（erp-stock-take 功能业务码；所选商品已有库存变动）</summary>
    public const int StockInitialNotAllowed = 40111;

    /// <summary>核销金额超过单据未结金额（erp-settlement 功能业务码；message 含单号与未结金额）</summary>
    public const int SettlementAmountExceeded = 40112;

    /// <summary>核销单据的往来单位与收付款单不一致（erp-settlement 功能业务码）</summary>
    public const int SettlementPartnerMismatch = 40113;

    /// <summary>收付款方向与单据类型不匹配（erp-settlement 功能业务码，如收款单核销采购入库单）</summary>
    public const int SettlementDirectionMismatch = 40114;

    /// <summary>本次数量超过订单未执行数量（erp-order-flow 功能业务码；message 含订单号、商品名与未执行数量）</summary>
    public const int OrderFulfillExceeded = 40115;

    /// <summary>订单当前状态不允许该操作（erp-order-flow 功能业务码；编辑 / 作废 / 关闭 / 关联收发货，message 说明当前状态）</summary>
    public const int OrderStateInvalid = 40116;

    /// <summary>出入库单的往来单位与所关联订单不一致（erp-order-flow 功能业务码）</summary>
    public const int OrderPartnerMismatch = 40117;

    /// <summary>成本重算正在进行，请稍后重试（erp-cost 功能业务码；内存锁并发拒绝）</summary>
    public const int CostRecalculationRunning = 40118;

    /// <summary>往来单位类型不允许收窄（erp-partner 功能业务码；只可保持原类型或改为两者）</summary>
    public const int PartnerTypeNarrowingNotAllowed = 40119;

    /// <summary>单据已被收付款单核销，禁止作废（erp-settlement 功能业务码；message 含单号与已结金额）</summary>
    public const int OrderSettledCannotVoid = 40120;
}
