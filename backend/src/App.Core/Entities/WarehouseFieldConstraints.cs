namespace App.Core.Entities;

/// <summary>
/// 仓库字段约束的**单一来源**（specs/038-erp-multi-warehouse/design.md §2.6）：
/// EF 实体配置（<c>HasMaxLength</c>）与各 <c>RequestValidator</c> 均引用本类常量，
/// 保证"格式校验"与"数据库约束"始终一致，避免同一字段在不同用例中规则分叉。
/// 与往来单位同义的字段（名称 / 地址 / 联系人 / 电话 / 备注）一律引用既有常量类，不复制数值。
/// </summary>
public static class WarehouseFieldConstraints
{
    /// <summary>仓库编码最短长度</summary>
    public const int CodeMinLength = 2;

    /// <summary>仓库编码最大长度（对齐 Warehouses.Code varchar(20)）</summary>
    public const int CodeMaxLength = 20;

    /// <summary>仓库编码格式：2–20 位字母 / 数字 / 下划线 / 连字符（同商品编码形态，长度不同故自建）</summary>
    public const string CodePattern = @"^[A-Za-z0-9_-]{2,20}$";

    /// <summary>仓库名称最短长度（与往来单位名称同源）</summary>
    public const int NameMinLength = PartnerFieldConstraints.NameMinLength;

    /// <summary>仓库名称最大长度（对齐 Warehouses.Name varchar(50)，与往来单位名称同源）</summary>
    public const int NameMaxLength = PartnerFieldConstraints.NameMaxLength;

    /// <summary>地址最大长度（与往来单位地址同源）</summary>
    public const int AddressMaxLength = PartnerFieldConstraints.AddressMaxLength;

    /// <summary>联系人最大长度（与往来单位联系人同源）</summary>
    public const int ContactMaxLength = PartnerFieldConstraints.ContactMaxLength;

    /// <summary>联系电话最大长度（与往来单位联系电话同源）</summary>
    public const int PhoneMaxLength = PartnerFieldConstraints.PhoneMaxLength;

    /// <summary>联系电话格式：11 位手机号（与往来单位同源）</summary>
    public const string PhonePattern = PartnerFieldConstraints.PhonePattern;

    /// <summary>备注最大长度（对齐 Warehouses.Remark varchar(200)）</summary>
    public const int RemarkMaxLength = OrderFieldConstraints.RemarkMaxLength;

    /// <summary>查询关键词最大长度（对齐被匹配列 Code(20) / Name(50) 的较大者，取 50）</summary>
    public const int KeywordMaxLength = 50;

    /// <summary>仓级安全库存阈值最小值（与商品安全库存同源）</summary>
    public const int SafetyStockMinValue = ProductFieldConstraints.SafetyStockMinValue;

    /// <summary>仓级安全库存阈值最大值（与商品安全库存同源）</summary>
    public const int SafetyStockMaxValue = ProductFieldConstraints.SafetyStockMaxValue;
}
