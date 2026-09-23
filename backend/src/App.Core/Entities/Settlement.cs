namespace App.Core.Entities;

/// <summary>
/// 收付款单实体（对应 PostgreSQL 表 Settlements，specs/023-erp-settlement/design.md §2.1）。
/// 一张收付款单可核销多张单据（明细见 <see cref="SettlementItem"/>），支持部分核销；可作废（回退已结算金额）。
/// 往来单位名称为快照；总额由后端按 Σ 核销金额重算（不信任前端传值）。
/// </summary>
public sealed class Settlement
{
    /// <summary>收付款单 ID</summary>
    public Guid Id { get; set; }

    /// <summary>单号，唯一，后端生成（收款 RC / 付款 PY + yyyyMMdd + 4 位序号，如 RC202609170001）</summary>
    public string SettlementNo { get; set; } = string.Empty;

    /// <summary>类型（0=收款 1=付款）</summary>
    public SettlementType Type { get; set; }

    /// <summary>往来单位 ID（外键 → Partners(Id)；收款为客户的，付款为供应商）</summary>
    public Guid PartnerId { get; set; }

    /// <summary>往来单位名称快照（列表 / 详情免 join）</summary>
    public string PartnerName { get; set; } = string.Empty;

    /// <summary>业务日期（UTC 午夜）</summary>
    public DateTimeOffset SettlementDate { get; set; }

    /// <summary>总额 = Σ 核销金额（后端重算，不信任前端传值）</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>方式（0=现金 1=银行转账 2=其他）</summary>
    public SettlementMethod Method { get; set; }

    /// <summary>
    /// 资金账户 id（外键 → BankAccounts(Id)，可空；`Other` 结算方式不关联账户）。
    /// 引入见 specs/034-erp-cash/design.md §2.2，类型匹配规则见同节 §0.1
    /// </summary>
    public Guid? BankAccountId { get; set; }

    /// <summary>单据状态（1=正常 0=已作废；作废后禁止再操作，作废回退各单据已结算金额）</summary>
    public OrderStatus Status { get; set; } = OrderStatus.Normal;

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>创建人用户 id</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>更新人用户 id</summary>
    public Guid? UpdatedBy { get; set; }
}
