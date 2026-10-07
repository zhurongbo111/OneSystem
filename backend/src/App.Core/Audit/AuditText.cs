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

    /// <summary>报价单状态文案（`037`：草稿 / 已转订单 / 已作废）</summary>
    /// <param name="status">报价单状态</param>
    public static string QuotationStatus(QuotationStatus status) => status switch
    {
        Entities.QuotationStatus.Draft => "草稿",
        Entities.QuotationStatus.Converted => "已转订单",
        Entities.QuotationStatus.Voided => "已作废",
        _ => AuditSummary.Empty,
    };

    /// <summary>线索状态文案（`043`：新线索 / 跟进中 / 已转化 / 已废弃）</summary>
    /// <param name="status">线索状态</param>
    public static string LeadStatus(LeadStatus status) => status switch
    {
        Entities.LeadStatus.New => "新线索",
        Entities.LeadStatus.Following => "跟进中",
        Entities.LeadStatus.Converted => "已转化",
        Entities.LeadStatus.Abandoned => "已废弃",
        _ => AuditSummary.Empty,
    };

    /// <summary>线索来源文案（`043`）</summary>
    /// <param name="source">线索来源</param>
    public static string LeadSource(LeadSource source) => source switch
    {
        Entities.LeadSource.Website => "网站",
        Entities.LeadSource.Phone => "电话",
        Entities.LeadSource.Referral => "推荐",
        Entities.LeadSource.Exhibition => "展会",
        Entities.LeadSource.Other => "其他",
        _ => AuditSummary.Empty,
    };

    /// <summary>商机阶段文案（`043`：初步接洽 / 需求确认 / 方案报价 / 谈判 / 赢单 / 输单）</summary>
    /// <param name="stage">商机阶段</param>
    public static string OpportunityStage(OpportunityStage stage) => stage switch
    {
        Entities.OpportunityStage.Initial => "初步接洽",
        Entities.OpportunityStage.Requirement => "需求确认",
        Entities.OpportunityStage.Proposal => "方案报价",
        Entities.OpportunityStage.Negotiation => "谈判",
        Entities.OpportunityStage.Won => "赢单",
        Entities.OpportunityStage.Lost => "输单",
        _ => AuditSummary.Empty,
    };

    /// <summary>跟进活动方式文案（`043`：电话 / 拜访 / 邮件 / 其他）</summary>
    /// <param name="type">跟进方式</param>
    public static string ActivityType(ActivityType type) => type switch
    {
        Entities.ActivityType.Call => "电话",
        Entities.ActivityType.Visit => "拜访",
        Entities.ActivityType.Email => "邮件",
        Entities.ActivityType.Other => "其他",
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

    /// <summary>单据审批状态文案（042）</summary>
    /// <param name="status">审批状态</param>
    public static string ApprovalStatus(Entities.ApprovalStatus status) => status switch
    {
        Entities.ApprovalStatus.None => "无需审批",
        Entities.ApprovalStatus.Pending => "待审批",
        Entities.ApprovalStatus.Approved => "已通过",
        Entities.ApprovalStatus.Rejected => "已驳回",
        Entities.ApprovalStatus.Withdrawn => "已撤回",
        _ => AuditSummary.Empty,
    };

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

    /// <summary>发票类型文案</summary>
    /// <param name="type">发票类型</param>
    public static string InvoiceType(InvoiceType type)
        => type == Entities.InvoiceType.Purchase ? "进项" : "销项";

    /// <summary>会计科目类别文案</summary>
    /// <param name="category">科目类别</param>
    public static string AccountCategory(AccountCategory category) => category switch
    {
        Entities.AccountCategory.Asset => "资产",
        Entities.AccountCategory.Liability => "负债",
        Entities.AccountCategory.Equity => "权益",
        Entities.AccountCategory.Cost => "成本",
        Entities.AccountCategory.ProfitLoss => "损益",
        _ => AuditSummary.Empty,
    };

    /// <summary>会计科目余额方向文案</summary>
    /// <param name="direction">余额方向</param>
    public static string AccountDirection(AccountDirection direction)
        => direction == Entities.AccountDirection.Debit ? "借" : "贷";

    /// <summary>会计科目状态文案</summary>
    /// <param name="status">状态</param>
    public static string AccountStatus(AccountStatus status)
        => status == Entities.AccountStatus.Enabled ? "启用" : "停用";

    /// <summary>税率状态文案</summary>
    /// <param name="status">状态</param>
    public static string TaxRateStatus(TaxRateStatus status)
        => status == Entities.TaxRateStatus.Enabled ? "启用" : "停用";

    /// <summary>资金账户类型文案</summary>
    /// <param name="type">账户类型</param>
    public static string BankAccountType(BankAccountType type) => type switch
    {
        Entities.BankAccountType.Cash => "现金",
        Entities.BankAccountType.Bank => "银行",
        _ => AuditSummary.Empty,
    };

    /// <summary>资金账户状态文案</summary>
    /// <param name="status">状态</param>
    public static string BankAccountStatus(BankAccountStatus status)
        => status == Entities.BankAccountStatus.Enabled ? "启用" : "停用";

    /// <summary>考勤类型文案（044：请假 / 加班）</summary>
    /// <param name="type">考勤类型</param>
    public static string AttendanceType(AttendanceType type) => type switch
    {
        Entities.AttendanceType.Leave => "请假",
        Entities.AttendanceType.Overtime => "加班",
        _ => AuditSummary.Empty,
    };

    /// <summary>工资单状态文案（044：草稿 / 已发放）</summary>
    /// <param name="status">工资单状态</param>
    public static string PayrollStatus(PayrollStatus status) => status switch
    {
        Entities.PayrollStatus.Draft => "草稿",
        Entities.PayrollStatus.Paid => "已发放",
        _ => AuditSummary.Empty,
    };

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
