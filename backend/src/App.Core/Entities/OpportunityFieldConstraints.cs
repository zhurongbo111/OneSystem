namespace App.Core.Entities;

/// <summary>
/// 商机字段约束常量（单一来源，specs/043-erp-crm-presale design.md §2.2 / §2.4）：
/// EF 配置（<c>OpportunityConfiguration</c>）与各用例 RequestValidator 均取本类常量，禁止硬编码字面量。
/// 客户名称快照列长复用 <see cref="PartnerFieldConstraints.NameMaxLength"/>，不在此重复登记。
/// </summary>
public static class OpportunityFieldConstraints
{
    /// <summary>商机单号最大长度（列 <c>varchar(20)</c>）</summary>
    public const int NoMaxLength = 20;

    /// <summary>商机名称最大长度（列 <c>varchar(50)</c>）</summary>
    public const int NameMaxLength = 50;

    /// <summary>备注最大长度（列 <c>varchar(200)</c>）</summary>
    public const int RemarkMaxLength = 200;

    /// <summary>
    /// 预计金额上界（= <c>numeric(18,2)</c> 可表示的最大值，0 为下界；越界由 Validator 拒绝为 40000）。
    /// </summary>
    public const decimal AmountMaxValue = 9999999999999999.99m;

    /// <summary>
    /// 查询关键词最大长度（等于列表模糊匹配列中的最大列长 <see cref="NameMaxLength"/>，超长不可能命中）。
    /// </summary>
    public const int KeywordMaxLength = NameMaxLength;
}
