using App.Core.Abstractions;

namespace App.Core.Features.Reports;

/// <summary>
/// 报表读模型 → 出参映射（正向映射；派生 / 计算字段——净额、库存占比——在本类内计算，
/// 读模型不暴露到 API，见后端规则 §4.3）。
/// </summary>
internal static class ReportsDtoMapper
{
    /// <summary>进销存报表行读模型 → 出参</summary>
    public static InventoryFlowItemDto ToInventoryFlowItemDto(InventoryFlowItem item)
        => new()
        {
            ProductId = item.ProductId.ToString(),
            Code = item.Code,
            Name = item.Name,
            CategoryName = item.CategoryName,
            Unit = item.Unit,
            OpeningQuantity = item.OpeningQuantity,
            InboundQuantity = item.InboundQuantity,
            OutboundQuantity = item.OutboundQuantity,
            ClosingQuantity = item.ClosingQuantity,
        };

    /// <summary>进销存报表合计读模型 → 出参</summary>
    public static InventoryFlowSummaryDto ToInventoryFlowSummaryDto(InventoryFlowTotal total)
        => new()
        {
            OpeningQuantity = total.OpeningQuantity,
            InboundQuantity = total.InboundQuantity,
            OutboundQuantity = total.OutboundQuantity,
            ClosingQuantity = total.ClosingQuantity,
        };

    /// <summary>
    /// 库存余额表行读模型 → 出参（库存占比 = 本分类库存 ÷ 全量筛选库存总量；全量为 0 时占比按 0 处理）
    /// </summary>
    /// <param name="item">余额行读模型</param>
    /// <param name="totalQuantity">全量筛选结果库存总量（分母）</param>
    public static StockBalanceItemDto ToStockBalanceItemDto(StockBalanceItem item, int totalQuantity)
        => new()
        {
            CategoryId = item.CategoryId.ToString(),
            CategoryName = item.CategoryName,
            ProductCount = item.ProductCount,
            TotalQuantity = item.TotalQuantity,
            ZeroStockCount = item.ZeroStockCount,
            BelowSafetyCount = item.BelowSafetyCount,
            QuantityRatio = totalQuantity > 0 ? (decimal)item.TotalQuantity / totalQuantity : 0m,
        };

    /// <summary>库存余额表合计读模型 → 出参</summary>
    public static StockBalanceSummaryDto ToStockBalanceSummaryDto(StockBalanceTotal total)
        => new()
        {
            ProductCount = total.ProductCount,
            TotalQuantity = total.TotalQuantity,
            ZeroStockCount = total.ZeroStockCount,
            BelowSafetyCount = total.BelowSafetyCount,
        };

    /// <summary>采购汇总行读模型 → 出参（净数量 / 净额 = 入库 − 退货）</summary>
    public static PurchaseSummaryItemDto ToPurchaseSummaryItemDto(PurchaseSummaryItem item)
        => new()
        {
            Key = item.Key?.ToString(),
            Name = item.Name,
            Unit = item.Unit,
            OrderCount = item.OrderCount,
            InboundQuantity = item.InboundQuantity,
            InboundAmount = item.InboundAmount,
            ReturnQuantity = item.ReturnQuantity,
            ReturnAmount = item.ReturnAmount,
            NetQuantity = item.InboundQuantity - item.ReturnQuantity,
            NetAmount = item.InboundAmount - item.ReturnAmount,
        };

    /// <summary>采购汇总合计读模型 → 出参</summary>
    public static PurchaseSummaryTotalDto ToPurchaseSummaryTotalDto(PurchaseSummaryTotal total)
        => new()
        {
            OrderCount = total.OrderCount,
            InboundQuantity = total.InboundQuantity,
            InboundAmount = total.InboundAmount,
            ReturnQuantity = total.ReturnQuantity,
            ReturnAmount = total.ReturnAmount,
            NetQuantity = total.InboundQuantity - total.ReturnQuantity,
            NetAmount = total.InboundAmount - total.ReturnAmount,
        };

    /// <summary>销售汇总行读模型 → 出参（净数量 / 净额 = 出库 − 退货）</summary>
    public static SalesSummaryItemDto ToSalesSummaryItemDto(SalesSummaryItem item)
        => new()
        {
            Key = item.Key?.ToString(),
            Name = item.Name,
            Unit = item.Unit,
            OrderCount = item.OrderCount,
            OutboundQuantity = item.OutboundQuantity,
            OutboundAmount = item.OutboundAmount,
            ReturnQuantity = item.ReturnQuantity,
            ReturnAmount = item.ReturnAmount,
            NetQuantity = item.OutboundQuantity - item.ReturnQuantity,
            NetAmount = item.OutboundAmount - item.ReturnAmount,
        };

    /// <summary>销售汇总合计读模型 → 出参</summary>
    public static SalesSummaryTotalDto ToSalesSummaryTotalDto(SalesSummaryTotal total)
        => new()
        {
            OrderCount = total.OrderCount,
            OutboundQuantity = total.OutboundQuantity,
            OutboundAmount = total.OutboundAmount,
            ReturnQuantity = total.ReturnQuantity,
            ReturnAmount = total.ReturnAmount,
            NetQuantity = total.OutboundQuantity - total.ReturnQuantity,
            NetAmount = total.OutboundAmount - total.ReturnAmount,
        };
}
