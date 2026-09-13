namespace App.Core.Entities;

/// <summary>
/// 往来单位字段约束的**单一来源**：EF 实体配置（<c>HasMaxLength</c>）与各 <c>RequestValidator</c> 均引用本类常量，
/// 保证"格式校验"与"数据库约束"始终一致，避免同一字段在不同用例中规则分叉。
/// </summary>
public static class PartnerFieldConstraints
{
    /// <summary>单位名称最短长度</summary>
    public const int NameMinLength = 1;

    /// <summary>单位名称最大长度（对齐 Partners.Name varchar(50)）</summary>
    public const int NameMaxLength = 50;

    /// <summary>联系人最大长度（对齐 Partners.Contact varchar(20)）</summary>
    public const int ContactMaxLength = 20;

    /// <summary>联系电话最大长度（对齐 Partners.Phone varchar(20)）</summary>
    public const int PhoneMaxLength = 20;

    /// <summary>联系电话格式：11 位手机号（与用户手机号同语义，各自常量类定义）</summary>
    public const string PhonePattern = @"^1[3-9]\d{9}$";

    /// <summary>地址最大长度（对齐 Partners.Address varchar(100)）</summary>
    public const int AddressMaxLength = 100;

    /// <summary>备注最大长度（对齐 Partners.Remark varchar(200)）</summary>
    public const int RemarkMaxLength = 200;

    /// <summary>查询关键词最大长度（对齐 Name 列长，取 50）</summary>
    public const int KeywordMaxLength = 50;
}
