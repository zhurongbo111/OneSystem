namespace App.Core.Entities;

/// <summary>
/// 商品分类字段约束的**单一来源**：EF 实体配置（<c>HasMaxLength</c>）与各 <c>RequestValidator</c> 均引用本类常量，
/// 保证"格式校验"与"数据库约束"始终一致，避免同一字段在不同用例中规则分叉。
/// </summary>
public static class CategoryFieldConstraints
{
    /// <summary>分类名称最短长度</summary>
    public const int NameMinLength = 1;

    /// <summary>分类名称最大长度（对齐 Categories.Name varchar(20)）</summary>
    public const int NameMaxLength = 20;
}
