namespace App.Core.Entities;

/// <summary>
/// 盘点 / 期初建账明细实体（对应 PostgreSQL 表 StockTakeItems）。
/// 逐行记录商品、账面数量、实盘数量与差异；实盘数量为 0 的行照常落库（库存设为 0）。
/// 编码 / 名称 / 单位为提交时快照；明细不可改、不软删除（一步式）。
/// </summary>
public sealed class StockTakeItem
{
    /// <summary>明细 ID</summary>
    public Guid Id { get; set; }

    /// <summary>所属盘点单 ID</summary>
    public Guid StockTakeId { get; set; }

    /// <summary>商品 ID</summary>
    public Guid ProductId { get; set; }

    /// <summary>商品编码（提交时快照，商品后续改名 / 改码不影响历史单据）</summary>
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>商品名称（提交时快照）</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>计量单位（提交时快照，NOT NULL）</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>账面数量（提交时后端读取，≥ 0）</summary>
    public int BookQuantity { get; set; }

    /// <summary>实盘数量（前端录入，≥ 0）</summary>
    public int ActualQuantity { get; set; }

    /// <summary>差异 = ActualQuantity − BookQuantity（后端计算，可负）</summary>
    public int Difference { get; set; }
}
