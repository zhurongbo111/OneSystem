namespace App.Core.Entities;

/// <summary>
/// 商品字段约束的**单一来源**：EF 实体配置（<c>HasMaxLength</c> / 精度）与各 <c>RequestValidator</c> 均引用本类常量，
/// 保证"格式校验"与"数据库约束"始终一致，避免同一字段在不同用例中规则分叉。
/// 价格 / 数量常量后续由 erp-purchase / erp-sale 的单价 / 小计 / 总额 / 明细数量共用，禁止另行硬编码。
/// </summary>
public static class ProductFieldConstraints
{
    /// <summary>商品编码最短长度</summary>
    public const int CodeMinLength = 2;

    /// <summary>商品编码最大长度（对齐 Products.Code varchar(32)）</summary>
    public const int CodeMaxLength = 32;

    /// <summary>商品编码格式：2–32 位字母 / 数字 / 下划线 / 连字符</summary>
    public const string CodePattern = @"^[A-Za-z0-9_-]{2,32}$";

    /// <summary>商品名称最短长度</summary>
    public const int NameMinLength = 2;

    /// <summary>商品名称最大长度（对齐 Products.Name varchar(50)）</summary>
    public const int NameMaxLength = 50;

    /// <summary>计量单位最大长度（对齐 Products.Unit varchar(10)）</summary>
    public const int UnitMaxLength = 10;

    /// <summary>价格最小值（采购价 / 销售价共用）</summary>
    public const decimal PriceMinValue = 0m;

    /// <summary>价格最大值（对齐 numeric(18,2) 展示上限；采购价 / 销售价共用）</summary>
    public const decimal PriceMaxValue = 9999999.99m;

    /// <summary>单据明细数量最小值（预留，供 erp-purchase / erp-sale 引用）</summary>
    public const int QuantityMinValue = 1;

    /// <summary>单据明细数量最大值（预留，供 erp-purchase / erp-sale 引用）</summary>
    public const int QuantityMaxValue = 999999;

    /// <summary>安全库存阈值最小值</summary>
    public const int SafetyStockMinValue = 0;

    /// <summary>安全库存阈值最大值</summary>
    public const int SafetyStockMaxValue = 999999;

    /// <summary>备注最大长度（对齐 Products.Remark varchar(200)）</summary>
    public const int RemarkMaxLength = 200;

    /// <summary>查询关键词最大长度（对齐 Code / Name 列长的较大约束，取 50）</summary>
    public const int KeywordMaxLength = 50;
}
