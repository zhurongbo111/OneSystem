namespace App.Core.Entities;

/// <summary>
/// 单据字段约束的**单一来源**（采购单 / 销售单共用）：EF 实体配置（<c>HasMaxLength</c> / 精度）
/// 与各 <c>RequestValidator</c> 均引用本类常量，禁止硬编码、禁止复制。
/// 明细行的数量 / 单价边界引用 <see cref="ProductFieldConstraints"/>（同一规则同源），禁止另行定义。
/// </summary>
public static class OrderFieldConstraints
{
    /// <summary>单据编号最大长度（对齐 PurchaseReceipts.OrderNo / SalesShipments.OrderNo varchar(20)）</summary>
    public const int OrderNoMaxLength = 20;

    /// <summary>备注最大长度（对齐 PurchaseReceipts.Remark / SalesShipments.Remark varchar(200)）</summary>
    public const int RemarkMaxLength = 200;

    /// <summary>单号查询关键词最大长度（对齐 OrderNo 列长）</summary>
    public const int KeywordMaxLength = 20;

    /// <summary>单张单据明细行数上限</summary>
    public const int ItemsMaxCount = 100;
}
