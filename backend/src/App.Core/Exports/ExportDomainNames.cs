namespace App.Core.Exports;

/// <summary>
/// 各导出项的中文域名（唯一来源）：同时作为下载文件名前缀（<c>&lt;域&gt;_&lt;yyyyMMddHHmm&gt;.xlsx</c>）
/// 与工作表名，避免各用例重复硬编码文案。
/// </summary>
public static class ExportDomainNames
{
    /// <summary>商品列表</summary>
    public const string Products = "商品";

    /// <summary>往来单位列表</summary>
    public const string Partners = "往来单位";

    /// <summary>库存查询</summary>
    public const string Inventory = "库存";

    /// <summary>库存流水</summary>
    public const string StockMovements = "库存流水";

    /// <summary>采购入库</summary>
    public const string PurchaseReceipts = "采购入库";

    /// <summary>销售出库</summary>
    public const string SalesShipments = "销售出库";

    /// <summary>采购退货</summary>
    public const string PurchaseReturns = "采购退货";

    /// <summary>销售退货</summary>
    public const string SalesReturns = "销售退货";

    /// <summary>收付款单</summary>
    public const string Settlements = "收付款";

    /// <summary>库存盘点</summary>
    public const string StockTakes = "库存盘点";

    /// <summary>进销存报表</summary>
    public const string InventoryFlow = "进销存报表";

    /// <summary>库存余额表</summary>
    public const string StockBalance = "库存余额表";

    /// <summary>采购汇总</summary>
    public const string PurchaseSummary = "采购汇总";

    /// <summary>销售汇总</summary>
    public const string SalesSummary = "销售汇总";

    /// <summary>成本与毛利</summary>
    public const string CostProfit = "成本与毛利";

    /// <summary>员工档案</summary>
    public const string Employees = "员工档案";

    /// <summary>发票登记</summary>
    public const string Invoices = "发票";

    /// <summary>单据类导出的「单据」工作表名（明细表见 <see cref="DetailSheet"/>）</summary>
    public const string DocumentSheet = "单据";

    /// <summary>单据类导出的「明细」工作表名</summary>
    public const string DetailSheet = "明细";
}
