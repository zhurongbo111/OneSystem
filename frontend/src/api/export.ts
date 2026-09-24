import type { GetEmployeesParams } from './employee'
import type { InventoryListQuery } from './inventory'
import type { InvoiceQuery } from './invoice'
import type { GetPartnersParams } from './partner'
import type { PartnerPriceQuery } from './partnerPrice'
import type { ProductListQuery } from './product'
import type { PurchaseReceiptQuery } from './purchase'
import type { PurchaseReturnQuery } from './purchaseReturn'
import { downloadBlob } from './request'
import type { SalesShipmentQuery } from './sale'
import type { SalesReturnQuery } from './saleReturn'
import type { SettlementQuery } from './settlement'
import type { StockMovementQuery } from './stockMovement'
import type { StockTakeQuery } from './stockTake'
import type { TransferQuery } from './transfer'

/**
 * 列表导出 Excel（erp-export）：导出**当前筛选条件下的全量数据**（不受分页限制，服务端生成 xlsx）。
 *
 * 归属说明（前端规则 §3）：导出是横跨所有域的横向能力，集中在本文件；
 * 报表导出并入同域的 `api/report.ts`。二进制下载与契约例外分流的唯一实现在 `api/request.ts` 的 `downloadBlob`。
 * 参数一律复用各域列表查询类型（去掉分页：后端忽略分页，只按筛选取全量）。
 */

/** 导出商品列表 */
export function exportProducts(params: Omit<ProductListQuery, 'page' | 'pageSize'>): Promise<void> {
  return downloadBlob('/products/export', params)
}

/** 导出发票（工作表：发票 + 关联明细） */
export function exportInvoices(params: Omit<InvoiceQuery, 'page' | 'pageSize'>): Promise<void> {
  return downloadBlob('/invoices/export', params)
}

/** 导出往来单位列表 */
export function exportPartners(params: Omit<GetPartnersParams, 'page' | 'pageSize'>): Promise<void> {
  return downloadBlob('/partners/export', params)
}

/** 导出客户价格列表（工作表：客户价格，含商品销售价对比） */
export function exportPartnerPrices(params: Omit<PartnerPriceQuery, 'page' | 'pageSize'>): Promise<void> {
  return downloadBlob('/partner-prices/export', params)
}

/** 导出库存查询列表 */
export function exportInventory(params: Omit<InventoryListQuery, 'page' | 'pageSize'>): Promise<void> {
  return downloadBlob('/inventory/export', params)
}

/** 导出库存流水列表 */
export function exportStockMovements(params: Omit<StockMovementQuery, 'page' | 'pageSize'>): Promise<void> {
  return downloadBlob('/stock-movements/export', params)
}

/** 导出采购入库单（工作表：单据 + 明细） */
export function exportPurchaseReceipts(params: Omit<PurchaseReceiptQuery, 'page' | 'pageSize'>): Promise<void> {
  return downloadBlob('/purchase-receipts/export', params)
}

/** 导出销售出库单（工作表：单据 + 明细） */
export function exportSalesShipments(params: Omit<SalesShipmentQuery, 'page' | 'pageSize'>): Promise<void> {
  return downloadBlob('/sales-shipments/export', params)
}

/** 导出采购退货单（工作表：单据 + 明细） */
export function exportPurchaseReturns(params: Omit<PurchaseReturnQuery, 'page' | 'pageSize'>): Promise<void> {
  return downloadBlob('/purchase-returns/export', params)
}

/** 导出销售退货单（工作表：单据 + 明细） */
export function exportSalesReturns(params: Omit<SalesReturnQuery, 'page' | 'pageSize'>): Promise<void> {
  return downloadBlob('/sales-returns/export', params)
}

/** 导出收付款单（工作表：单据 + 核销明细） */
export function exportSettlements(params: Omit<SettlementQuery, 'page' | 'pageSize'>): Promise<void> {
  return downloadBlob('/settlements/export', params)
}

/** 导出库存盘点单（工作表：单据 + 明细） */
export function exportStockTakes(params: Omit<StockTakeQuery, 'page' | 'pageSize'>): Promise<void> {
  return downloadBlob('/stock-takes/export', params)
}

/** 导出员工档案列表 */
export function exportEmployees(params: Omit<GetEmployeesParams, 'page' | 'pageSize'>): Promise<void> {
  return downloadBlob('/employees/export', params)
}

/** 导出调拨单（工作表：单据 + 明细，design §0.2 续行 039） */
export function exportTransfers(params: Omit<TransferQuery, 'page' | 'pageSize'>): Promise<void> {
  return downloadBlob('/transfers/export', params)
}
