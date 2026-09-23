import { downloadBlob, get } from './request'

/**
 * 报表分页结果（对应后端 ReportPageDto<TItem, TSummary>）：
 * 在统一分页字段之上追加 summary 合计（全量筛选结果口径，不是当前页）。
 */
export interface ReportPage<TItem, TSummary> {
  items: TItem[]
  total: number
  page: number
  pageSize: number
  summary: TSummary
}

// ============================== 进销存报表 ==============================

/** 进销存报表行（对应后端 InventoryFlowItemDto） */
export interface InventoryFlowItem {
  productId: string
  code: string
  name: string
  categoryName: string
  unit: string
  openingQuantity: number
  inboundQuantity: number
  outboundQuantity: number
  closingQuantity: number
}

/** 进销存报表合计（对应后端 InventoryFlowSummaryDto） */
export interface InventoryFlowSummary {
  openingQuantity: number
  inboundQuantity: number
  outboundQuantity: number
  closingQuantity: number
}

/** 进销存报表查询参数（对应后端 GetInventoryFlowRequest；start / end 为 UTC ISO 串的半开区间） */
export interface InventoryFlowQuery {
  start: string
  end: string
  productId?: string
  categoryId?: string
  /** 仓库 id，可空（038；不传 = 全部仓合并） */
  warehouseId?: string
  onlyChanged?: boolean
  page: number
  pageSize: number
}

// ============================== 库存余额表 ==============================

/** 库存余额表行（对应后端 StockBalanceItemDto，按分类聚合） */
export interface StockBalanceItem {
  categoryId: string
  categoryName: string
  productCount: number
  totalQuantity: number
  zeroStockCount: number
  belowSafetyCount: number
  /** 库存占比（0–1，分母为全量筛选结果库存总量） */
  quantityRatio: number
  /** 库存成本额合计（erp-cost；展示收敛到 2 位） */
  totalCostAmount: number
  /** 移动加权平均单价（erp-cost；= 成本额 ÷ 库存，库存为 0 时为 0） */
  averageCost: number
  /** 成本异常（库存 &lt; 0 或成本额 &lt; 0；报表标红提示，不阻断业务） */
  hasCostAnomaly: boolean
}

/** 库存余额表合计（对应后端 StockBalanceSummaryDto） */
export interface StockBalanceSummary {
  productCount: number
  totalQuantity: number
  zeroStockCount: number
  belowSafetyCount: number
  /** 库存成本额合计（erp-cost） */
  totalCostAmount: number
}

/** 库存余额表查询参数（对应后端 GetStockBalanceRequest） */
export interface StockBalanceQuery {
  keyword?: string
  categoryId?: string
  /** 仓库 id，可空（038；不传 = 全部仓合并） */
  warehouseId?: string
  page: number
  pageSize: number
}

// ============================== 采购 / 销售汇总 ==============================

/** 汇总分组维度（对应后端 SummaryGroupBy） */
export type SummaryGroupBy = 'partner' | 'product'

/** 采购汇总行（对应后端 PurchaseSummaryItemDto；净额由后端计算） */
export interface PurchaseSummaryItem {
  key: string
  name: string
  unit: string | null
  orderCount: number
  inboundQuantity: number
  inboundAmount: number
  returnQuantity: number
  returnAmount: number
  netQuantity: number
  netAmount: number
}

/** 采购汇总合计（对应后端 PurchaseSummaryTotalDto） */
export interface PurchaseSummaryTotal {
  orderCount: number
  inboundQuantity: number
  inboundAmount: number
  returnQuantity: number
  returnAmount: number
  netQuantity: number
  netAmount: number
}

/** 采购汇总查询参数（对应后端 GetPurchaseSummaryRequest） */
export interface PurchaseSummaryQuery {
  start: string
  end: string
  partnerId?: string
  groupBy: SummaryGroupBy
  page: number
  pageSize: number
}

/** 销售汇总行（对应后端 SalesSummaryItemDto；净额由后端计算） */
export interface SalesSummaryItem {
  key: string
  name: string
  unit: string | null
  orderCount: number
  outboundQuantity: number
  outboundAmount: number
  returnQuantity: number
  returnAmount: number
  netQuantity: number
  netAmount: number
}

/** 销售汇总合计（对应后端 SalesSummaryTotalDto） */
export interface SalesSummaryTotal {
  orderCount: number
  outboundQuantity: number
  outboundAmount: number
  returnQuantity: number
  returnAmount: number
  netQuantity: number
  netAmount: number
}

/** 销售汇总查询参数（对应后端 GetSalesSummaryRequest） */
export interface SalesSummaryQuery {
  start: string
  end: string
  partnerId?: string
  groupBy: SummaryGroupBy
  page: number
  pageSize: number
}

/**
 * 把页面选择的本地日期区间转换为后端所需的 UTC ISO **半开区间**：
 * 起始取当天本地 00:00:00、结束取「结束日次日」本地 00:00:00，再转 UTC。
 * 半开区间（start <= t < end）保证相邻区间拼接无重叠、无遗漏（specs/025-erp-report design.md §0.1）。
 */
export function toReportRangeUtc(startDate: string, endDate: string): { start: string; end: string } {
  const start = new Date(`${startDate}T00:00:00`)
  const endExclusive = new Date(`${endDate}T00:00:00`)
  endExclusive.setDate(endExclusive.getDate() + 1)
  return { start: start.toISOString(), end: endExclusive.toISOString() }
}

// ============================== 成本与毛利报表（erp-cost）=============================

/** 成本与毛利分组维度（对应后端 CostProfitGroupBy） */
export type CostProfitGroupBy = 'order' | 'product' | 'partner'

/** 成本与毛利报表行（对应后端 CostProfitItemDto；毛利由后端计算，毛利率收入为 0 时为 null） */
export interface CostProfitItem {
  /** 分组键（单据 / 商品 / 往来 id；后端恒有值） */
  key: string
  name: string
  salesQuantity: number
  salesAmount: number
  costAmount: number
  grossProfit: number
  grossProfitRate: number | null
  hasMissingCost: boolean
}

/** 成本与毛利报表合计（对应后端 CostProfitTotalDto；全量筛选口径） */
export interface CostProfitSummary {
  salesQuantity: number
  salesAmount: number
  costAmount: number
  grossProfit: number
  grossProfitRate: number | null
  hasMissingCost: boolean
}

/** 成本与毛利报表查询参数（对应后端 GetCostProfitReportRequest） */
export interface CostProfitQuery {
  start: string
  end: string
  productId?: string
  categoryId?: string
  groupBy: CostProfitGroupBy
  page: number
  pageSize: number
}

/** 成本与毛利报表：期间内销售出库与退货按维度聚合收入 / 成本 / 毛利（只读） */
export function getCostProfitReport(
  query: CostProfitQuery,
): Promise<ReportPage<CostProfitItem, CostProfitSummary>> {
  return get<ReportPage<CostProfitItem, CostProfitSummary>>('/reports/cost-profit', { params: query })
}

/** 进销存报表：期间 + 商品 / 分类 / 只看有变动（只读） */
export function getInventoryFlow(
  query: InventoryFlowQuery,
): Promise<ReportPage<InventoryFlowItem, InventoryFlowSummary>> {
  return get<ReportPage<InventoryFlowItem, InventoryFlowSummary>>('/reports/inventory-flow', { params: query })
}

/** 库存余额表：按分类聚合（只读） */
export function getStockBalance(
  query: StockBalanceQuery,
): Promise<ReportPage<StockBalanceItem, StockBalanceSummary>> {
  return get<ReportPage<StockBalanceItem, StockBalanceSummary>>('/reports/stock-balance', { params: query })
}

/** 采购汇总：按供应商 / 商品维度聚合（只读） */
export function getPurchaseSummary(
  query: PurchaseSummaryQuery,
): Promise<ReportPage<PurchaseSummaryItem, PurchaseSummaryTotal>> {
  return get<ReportPage<PurchaseSummaryItem, PurchaseSummaryTotal>>('/reports/purchase-summary', { params: query })
}

/** 销售汇总：按客户 / 商品维度聚合（只读） */
export function getSalesSummary(
  query: SalesSummaryQuery,
): Promise<ReportPage<SalesSummaryItem, SalesSummaryTotal>> {
  return get<ReportPage<SalesSummaryItem, SalesSummaryTotal>>('/reports/sales-summary', { params: query })
}

// ============================== 报表导出（erp-export）==============================

/**
 * 报表导出（erp-export）：导出**当前筛选条件下的全量数据 + 与页面一致的合计行**（服务端生成 xlsx）。
 * 与报表查询同域，故并入本文件（列表类导出集中在 `api/export.ts`）；
 * 二进制下载与契约例外分流见 `api/request.ts` 的 `downloadBlob`。
 */

/** 导出进销存报表（含合计行） */
export function exportInventoryFlow(params: Omit<InventoryFlowQuery, 'page' | 'pageSize'>): Promise<void> {
  return downloadBlob('/reports/inventory-flow/export', params)
}

/** 导出库存余额表（含合计行） */
export function exportStockBalance(params: Omit<StockBalanceQuery, 'page' | 'pageSize'>): Promise<void> {
  return downloadBlob('/reports/stock-balance/export', params)
}

/** 导出采购汇总（含合计行） */
export function exportPurchaseSummary(params: Omit<PurchaseSummaryQuery, 'page' | 'pageSize'>): Promise<void> {
  return downloadBlob('/reports/purchase-summary/export', params)
}

/** 导出销售汇总（含合计行） */
export function exportSalesSummary(params: Omit<SalesSummaryQuery, 'page' | 'pageSize'>): Promise<void> {
  return downloadBlob('/reports/sales-summary/export', params)
}

/** 导出成本与毛利报表（含合计行） */
export function exportCostProfit(params: Omit<CostProfitQuery, 'page' | 'pageSize'>): Promise<void> {
  return downloadBlob('/reports/cost-profit/export', params)
}
