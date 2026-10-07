namespace App.Core.Entities;

/// <summary>
/// 服务工单字段约束常量（单一来源，specs/045-erp-crm-service design.md §2.1 / §2.2）：
/// EF 配置（<c>ServiceTicketConfiguration</c>）与各用例 RequestValidator 均取本类常量，禁止硬编码字面量。
/// </summary>
public static class ServiceTicketFieldConstraints
{
    /// <summary>工单号最大长度（列 <c>varchar(20)</c>）</summary>
    public const int NoMaxLength = 20;

    /// <summary>工单标题最大长度（列 <c>varchar(50)</c>）</summary>
    public const int TitleMaxLength = 50;

    /// <summary>问题描述最大长度（列 <c>varchar(500)</c>）</summary>
    public const int DescriptionMaxLength = 500;

    /// <summary>联系人最大长度（列 <c>varchar(30)</c>）</summary>
    public const int ContactMaxLength = 30;

    /// <summary>联系电话最大长度（列 <c>varchar(20)</c>）</summary>
    public const int PhoneMaxLength = 20;

    /// <summary>备注最大长度（列 <c>varchar(200)</c>）</summary>
    public const int RemarkMaxLength = 200;

    /// <summary>
    /// 查询关键词最大长度（等于列表模糊匹配列中的最大列长 <see cref="TitleMaxLength"/>，超长不可能命中）。
    /// </summary>
    public const int KeywordMaxLength = TitleMaxLength;
}
