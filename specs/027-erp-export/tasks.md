---
created: 2026-09-17
updated: 2026-09-20
---

# 任务清单：列表导出 Excel 与单据打印（erp-export）

> 依据 `specs/027-erp-export/design.md` 拆分。含后端导出能力（15 个端点）+ 前端导出交互 + 6 个打印视图 + 契约例外登记。按顺序实现，完成后勾选。
> 前置：`012`–`026` 各域列表 / 报表已实现（导出复用其列表查询与列定义）。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：导出能力

- [x] 1.1 `App.Core/Exports/` 新增 `ExcelWorkbookModel` / `ExcelSheetModel` / `ExcelColumnModel` / `ExcelValueType` / `ExportFieldConstraints`（`MaxRows = 50000`）
- [x] 1.2 `App.Core/Abstractions/IExcelExporter.cs`（`Build`）
- [x] 1.3 `App.Infrastructure` 引入 `ClosedXML`；新增 `Exports/ClosedXmlExcelExporter.cs`（表头 / 冻结 / 列宽 / 格式化 / 合计行 / Sheet 名截断 / 单表上限）；`AddInfrastructure` 注册
- [x] 1.4 `ExportResultDto { FileName, Content }` + 各域 `Export<X>Request`（列表筛选字段、忽略分页）

## 二、后端：导出用例与端点（15 个）

- [x] 2.1 商品列表导出（`Products/ExportProducts` + `GET /api/products/export`）
- [x] 2.2 往来单位导出（`Partners/ExportPartners`）
- [x] 2.3 库存查询导出（`Inventory/ExportInventory`）
- [x] 2.4 库存流水导出（`StockMovements/ExportStockMovements`）
- [x] 2.5 采购入库导出（`PurchaseReceipts/ExportPurchaseReceipts`，2 工作表）
- [x] 2.6 销售出库导出（`SalesShipments/ExportSalesShipments`，2 工作表）
- [x] 2.7 采购退货导出（`PurchaseReturns/ExportPurchaseReturns`，2 工作表）
- [x] 2.8 销售退货导出（`SalesReturns/ExportSalesReturns`，2 工作表）
- [x] 2.9 收付款单导出（`Settlements/ExportSettlements`，2 工作表）
- [x] 2.10 库存盘点导出（`StockTakes/ExportStockTakes`，2 工作表）
- [x] 2.11 报表导出 5 个（`Reports/ExportInventoryFlow` / `ExportStockBalance` / `ExportPurchaseSummary` / `ExportSalesSummary` / `ExportCostProfit`，均含合计行）
- [x] 2.12 各 Controller 追加 `export` 动作（`File(...)` 文件流；`[ProducesResponseType]` 标注）；`App.Core/DependencyInjection.cs` 注册 15 个 Handler 与 Validator

## 三、单元测试（后端）

- [x] 3.1 各导出用例：筛选透传（`page=1` / `pageSize=MaxRows+1`）、超限 `40000`、边界恰好 `MaxRows` 通过
- [x] 3.2 表格模型映射：列头规则、单据 2 工作表且明细首列为单号、报表含合计行、空结果仅表头
- [x] 3.3 `ClosedXmlExcelExporter`：round-trip 可打开、Sheet 名截断、金额 2 位、超 Excel 单表上限 `40000`
- [x] 3.4 集成测试：导出响应 `FileResult` + xlsx Content-Type；参数非法返回统一响应 JSON（`40000`）
- [x] 3.5 `cd backend && dotnet build` / `dotnet test` 通过（既有用例全部回归）

## 四、前端：导出交互

- [x] 4.1 `api/request.ts` 追加 `downloadBlob(url, params)`（`responseType: blob` + `Content-Type` 分流 + 40100 统一处置）
- [x] 4.2 `src/api/export.ts`（10 个列表导出函数）；`src/api/report.ts` 追加 5 个报表导出函数
- [x] 4.3 各列表操作行接入「导出」按钮（`exporting` + 防重入 + 空数据提示），位置与分组遵循 `specs/006-list-showcase/design.md` §0
- [x] 4.4 `025` / `026` 报表页移除导出按钮的 `:disabled` 占位，接入真实导出
- [x] 4.5 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 五、前端：打印视图（6 个）

- [x] 5.1 `PurchaseManagement/PurchasePrintView.vue`（版式 + 打印工具条 + `@media print` 样式）
- [x] 5.2 `SalesManagement/SalePrintView.vue`
- [x] 5.3 `PurchaseReturnManagement/PurchaseReturnPrintView.vue`
- [x] 5.4 `SalesReturnManagement/SalesReturnPrintView.vue`
- [x] 5.5 `SettlementManagement/SettlementPrintView.vue`
- [x] 5.6 `StockTakeManagement/StockTakePrintView.vue`
- [x] 5.7 `router/index.ts` 新增 6 条顶层打印路由（不进 `AppLayout`、不进菜单）；各详情页头部 + 列表操作列接入「打印」入口（`IconPrinter`）
- [x] 5.8 打印样式抽取到 `src/utils/` 之外的方式确认（scoped 样式 + `print-page` 公共样式收敛到 `src/App.vue` 全局打印样式）

## 六、E2E（Playwright）

- [x] 6.1 新增 `e2e/export.spec.ts`：商品列表筛选后导出 → `download` 事件、文件名格式、文件非空
- [x] 6.2 同文件：采购入库导出（2 工作表 —— 断言下载成功与文件名；工作表内容通过后端单测覆盖）
- [x] 6.3 同文件：报表导出（含合计行，断言下载成功）
- [x] 6.4 新增打印断言（并入 `e2e/export.spec.ts`）：直达 `/print/purchases/:id` 渲染单号 / 供应商 / 明细，且**无侧边栏**；点击「打印」不报错
- [x] 6.5 `cd frontend && npm run test:e2e` 全量通过（含各域 spec 因操作行 / 操作列新增按钮的回归）

## 七、规格与上下文联动

- [x] 7.1 `AGENTS.md` §4.1 追加「文件下载类接口例外」条款（指针到本规格 §0.1）
- [x] 7.2 `specs/003-api-swagger/design.md` 加注记：`FileResult` 动作的文档标注方式
- [x] 7.3 `specs/006-list-showcase/design.md` §0 加注记：操作行「数据操作」组含导出（Excel，后端生成）；示例页 CSV 导出保留为演示
- [x] 7.4 `specs/011-action-column/design.md` §0 图标表续行「打印 `IconPrinter`」
- [x] 7.5 `specs/025` / `026` `design.md` 加注记：导出按钮由 `027` 启用（移除 `:disabled`）
- [x] 7.6 `.codebuddy/CONTEXT.md` §2（`Exports`/`IExcelExporter`/包依赖）、§3（`api/export.ts`、6 个打印视图与顶层路由）、§6 同步
- [x] 7.7 `specs/ROADMAP.md` 状态列更新（`027` → 已实现）

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 契约例外（文件下载）已在总则登记，前端统一由 `downloadBlob` 处理，无第二处响应分流实现。
