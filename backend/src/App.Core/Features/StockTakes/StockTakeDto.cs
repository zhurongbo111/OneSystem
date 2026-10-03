namespace App.Core.Features.StockTakes;

/// <summary>
/// 盘点 / 期初建账单出参共享模型（主表 + 明细，与后端 DTO camelCase 一一对应；列表行用 StockTakeListItemDto）。
/// </summary>
public sealed class StockTakeDetailDto
{
    /// <summary>盘点单 id</summary>
    public required string Id { get; init; }

    /// <summary>单号（ST + yyyyMMdd + 4 位序号）</summary>
    public required string TakeNo { get; init; }

    /// <summary>单据类型（0 期初建账 / 1 库存盘点）</summary>
    public required int Type { get; init; }

    /// <summary>盘点仓 id（038）</summary>
    public required string WarehouseId { get; init; }

    /// <summary>盘点仓名称快照（038）</summary>
    public required string WarehouseName { get; init; }

    /// <summary>盘点业务日期</summary>
    public required DateTimeOffset TakeDate { get; init; }

    /// <summary>明细行数</summary>
    public required int ItemCount { get; init; }

    /// <summary>差异行数（Difference != 0）</summary>
    public required int DiffItemCount { get; init; }

    /// <summary>备注（盘点说明 / 差异原因）</summary>
    public string? Remark { get; init; }

    /// <summary>创建人用户 id</summary>
    public string? CreatedBy { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>明细行（按插入顺序）</summary>
    public required IReadOnlyList<StockTakeItemDto> Items { get; init; }
}

/// <summary>盘点明细出参模型（快照字段原样返回，差异为后端重算值）</summary>
public sealed class StockTakeItemDto
{
    /// <summary>明细行 id</summary>
    public required string Id { get; init; }

    /// <summary>商品 id</summary>
    public required string ProductId { get; init; }

    /// <summary>批次 id（040：按批次盘点行有值，非批次行 null）</summary>
    public string? BatchId { get; init; }

    /// <summary>批次号快照（040；非批次行为 null）</summary>
    public string? BatchNo { get; init; }

    /// <summary>商品编码快照</summary>
    public required string ProductCode { get; init; }

    /// <summary>商品名称快照</summary>
    public required string ProductName { get; init; }

    /// <summary>计量单位快照</summary>
    public required string Unit { get; init; }

    /// <summary>账面数量（提交时后端读取）</summary>
    public required int BookQuantity { get; init; }

    /// <summary>实盘数量</summary>
    public required int ActualQuantity { get; init; }

    /// <summary>差异（实盘 − 账面，后端重算，可负）</summary>
    public required int Difference { get; init; }

    /// <summary>期初成本单价（erp-cost；仅期初建账明细有值，盘点明细为 0）</summary>
    public decimal UnitCost { get; init; }
}

/// <summary>盘点单列表行出参模型（差异行数供前端标橙）</summary>
public sealed class StockTakeListItemDto
{
    /// <summary>盘点单 id</summary>
    public required string Id { get; init; }

    /// <summary>单号</summary>
    public required string TakeNo { get; init; }

    /// <summary>单据类型（0 期初建账 / 1 库存盘点）</summary>
    public required int Type { get; init; }

    /// <summary>盘点仓 id（038）</summary>
    public required string WarehouseId { get; init; }

    /// <summary>盘点仓名称快照（038）</summary>
    public required string WarehouseName { get; init; }

    /// <summary>盘点业务日期</summary>
    public required DateTimeOffset TakeDate { get; init; }

    /// <summary>明细行数</summary>
    public required int ItemCount { get; init; }

    /// <summary>差异行数（Difference != 0，前端 > 0 标橙）</summary>
    public required int DiffItemCount { get; init; }

    /// <summary>备注</summary>
    public string? Remark { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// 盘点商品选择出参模型（启用商品 + 当前库存 + 是否已发生库存变动；期初模式据此标注「已建账」并禁用）。
/// 040：按批次商品带出该仓批次行（<see cref="Batches"/>），前端按批次拆行盘点。
/// </summary>
public sealed class StockTakeProductPickDto
{
    /// <summary>商品 id</summary>
    public required string Id { get; init; }

    /// <summary>商品编码</summary>
    public required string Code { get; init; }

    /// <summary>商品名称</summary>
    public required string Name { get; init; }

    /// <summary>计量单位</summary>
    public required string Unit { get; init; }

    /// <summary>是否按批次管理（040：true 时前端按 <see cref="Batches"/> 逐批次拆行）</summary>
    public required bool IsBatchManaged { get; init; }

    /// <summary>当前库存（账面，供录入参考；按批次商品为 0，批次行见 <see cref="Batches"/>）</summary>
    public required int StockQuantity { get; init; }

    /// <summary>是否已发生库存变动（true = 已建账 / 已有单据业务，期初模式禁用；按批次商品为 false，批次行见 <see cref="Batches"/>）</summary>
    public required bool HasMovements { get; init; }

    /// <summary>该仓批次行（仅按批次商品非空）</summary>
    public IReadOnlyList<StockTakeBatchPickDto>? Batches { get; init; }
}

/// <summary>盘点批次行出参模型（按批次盘点拆行用，040）</summary>
public sealed class StockTakeBatchPickDto
{
    /// <summary>批次 id</summary>
    public required string Id { get; init; }

    /// <summary>批次号</summary>
    public required string BatchNo { get; init; }

    /// <summary>该仓账面数量（供录入参考）</summary>
    public required int StockQuantity { get; init; }

    /// <summary>该仓是否已发生库存变动（期初模式据此禁用）</summary>
    public required bool HasMovements { get; init; }
}
