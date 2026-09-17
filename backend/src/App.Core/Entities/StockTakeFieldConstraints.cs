namespace App.Core.Entities;

/// <summary>
/// 盘点 / 期初建账字段约束的**单一来源**：EF 实体配置（<c>HasMaxLength</c>）与各 <c>RequestValidator</c>
/// 均引用本类常量，禁止硬编码、禁止复制。
/// 除「实盘数量下限」外（实盘可为 0，与单据明细数量下限 <c>ProductFieldConstraints.QuantityMinValue = 1</c> 语义不同），
/// 其余常量一律引用商品域 / 单据域既有常量（同一规则同源），禁止另行定义数值。
/// </summary>
public static class StockTakeFieldConstraints
{
    /// <summary>实盘数量最小值（本域定义：实盘允许为 0，语义区别于单据明细数量下限 1）</summary>
    public const int ActualQuantityMinValue = 0;

    /// <summary>实盘数量最大值（引用商品域数量上界，禁止复制数值）</summary>
    public const int ActualQuantityMaxValue = ProductFieldConstraints.QuantityMaxValue;

    /// <summary>单号最大长度（引用单据域单号列长，对齐 StockTakes.TakeNo varchar(20)）</summary>
    public const int TakeNoMaxLength = OrderFieldConstraints.OrderNoMaxLength;

    /// <summary>备注最大长度（引用单据域备注列长，对齐 StockTakes.Remark varchar(200)）</summary>
    public const int RemarkMaxLength = OrderFieldConstraints.RemarkMaxLength;

    /// <summary>单号查询关键词最大长度（引用单据域，对齐实际匹配列 TakeNo）</summary>
    public const int KeywordMaxLength = OrderFieldConstraints.KeywordMaxLength;

    /// <summary>单张盘点单明细行数上限（引用单据域，同一种「明细行数上限」概念）</summary>
    public const int ItemsMaxCount = OrderFieldConstraints.ItemsMaxCount;
}
