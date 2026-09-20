---
created: 2026-09-17
updated: 2026-09-20
---

# 设计规格：列表导出 Excel 与单据打印（erp-export）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织：导出用例归属**各业务域**（`Features/<域>/Export<X>`），不在 Core 新建跨域「导出域」。
> 本规格为**横向能力**：不新增业务表、不改变任何既有写入路径。

## 0. 约定正文（唯一事实源）

### 0.1 文件下载类接口的契约例外

**例外**：导出接口成功时返回**二进制文件流**，不套 `{ code, message, data }`（`AGENTS.md` §4.1 的例外，需在总则登记，见 `tasks.md` §联动）。

| 场景 | 响应 |
|---|---|
| 成功 | HTTP 200 + `Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` + `Content-Disposition: attachment; filename*=UTF-8''<编码文件名>` |
| 参数非法（含导出上限超限） | 照常走全局异常 / 校验管道 → HTTP 200 + 统一响应 JSON（`40000`） |
| 服务端异常 | HTTP 200 + 统一响应 JSON（`50000`） |

- **前端区分方式**：`Content-Type` 含 `json` → 按统一响应解包并 `Message.error`；否则按 `Blob` 下载。
- 文件名：`<域>_<yyyyMMddHHmm>.xlsx`（中文域名，`Content-Disposition` 用 `filename*` UTF-8 编码；前端 `Blob` 下载时以解析出的文件名落盘）。
- 本例外**只适用于文件下载接口**（当前仅本规格的导出）；其余接口一律遵循统一响应。

### 0.2 导出范围与内容

> 导出列集 = **该域列表的列定义**（各域 `design.md` §4.4 为准）去掉**序号列**与**操作列**，并追加 `创建人` / `创建时间`（单据类已有创建时间则不重复）；金额 2 位小数、数量整数、日期 `YYYY-MM-DD`、时间 `YYYY-MM-DD HH:mm`。**不在本表复制各域列清单**（单一事实源在各域规格）。

| 导出项 | 导出端点（各域 Controller 追加） | 用例目录 | 工作表 |
|---|---|---|---|
| 商品列表 | `GET /api/products/export` | `Products/ExportProducts` | 1 张 |
| 往来单位列表 | `GET /api/partners/export` | `Partners/ExportPartners` | 1 张 |
| 库存查询 | `GET /api/inventory/export` | `Inventory/ExportInventory` | 1 张 |
| 库存流水 | `GET /api/stock-movements/export` | `StockMovements/ExportStockMovements` | 1 张 |
| 采购入库 | `GET /api/purchase-receipts/export` | `PurchaseReceipts/ExportPurchaseReceipts` | 2 张（单据 + 明细） |
| 销售出库 | `GET /api/sales-shipments/export` | `SalesShipments/ExportSalesShipments` | 2 张 |
| 采购退货 | `GET /api/purchase-returns/export` | `PurchaseReturns/ExportPurchaseReturns` | 2 张 |
| 销售退货 | `GET /api/sales-returns/export` | `SalesReturns/ExportSalesReturns` | 2 张 |
| 收付款单 | `GET /api/settlements/export` | `Settlements/ExportSettlements` | 2 张（单据 + 核销明细） |
| 库存盘点 | `GET /api/stock-takes/export` | `StockTakes/ExportStockTakes` | 2 张（单据 + 明细） |
| 进销存报表 | `GET /api/reports/inventory-flow/export` | `Reports/ExportInventoryFlow` | 1 张（含合计行） |
| 库存余额表 | `GET /api/reports/stock-balance/export` | `Reports/ExportStockBalance` | 1 张（含合计行） |
| 采购汇总 | `GET /api/reports/purchase-summary/export` | `Reports/ExportPurchaseSummary` | 1 张（含合计行） |
| 销售汇总 | `GET /api/reports/sales-summary/export` | `Reports/ExportSalesSummary` | 1 张（含合计行） |
| 成本与毛利 | `GET /api/reports/cost-profit/export` | `Reports/ExportCostProfit` | 1 张（含合计行） |

- **导出参数 = 该列表 / 报表的既有筛选参数**（忽略 `page` / `pageSize`，后端按 `page = 1, pageSize = 上限` 取数）；单据类明细工作表首列固定为所属单号。
- **路由与用例名修正（对齐 `024-erp-order-flow` 落地的重命名，2026-09-20）**：本表起草早于 `024` 的表 / 接口重命名，原「采购入库 `purchase-orders` / `Purchases`」「销售出库 `sales-orders` / `Sales`」已失效；实现以现状域为准——采购入库 = `api/purchase-receipts` + `Features/PurchaseReceipts/ExportPurchaseReceipts`，销售出库 = `api/sales-shipments` + `Features/SalesShipments/ExportSalesShipments`（列表列定义与筛选参数同样取对应域 `design.md` §4.4）。
- **导出上限**：单次导出最多 `ExportFieldConstraints.MaxRows = 50000` 行（每个工作表独立计数），超限返回 `40000`（message 提示缩小筛选范围）。
- **分类管理不做导出**：单级字典（`017`），页面内已可直接维护，无交付价值（不做）。
- 排序与列表一致（各域既定排序）；作废单据 / 行**照常导出**（含状态列），便于对账核对。
- **枚举 / 比率 / 标记列的导出口径（2026-09-20 实现期收敛，唯一事实源）**：
  - 枚举列统一输出**中文文案**（`App.Core/Exports/ExportLabels`），与页面一致：商品 / 往来状态「启用 / 停用」、往来类型「供应商 / 客户 / 两者」、单据状态「正常 / 已作废」、变动类型取 `019` §0 十项文案、盘点类型「期初建账 / 库存盘点」、收付款「收款 / 付款」与方式「现金 / 银行转账 / 其他」、核销单据类型「采购入库单 / 销售出库单 / 采购退货单 / 销售退货单」。
  - 单据「结算状态」与列表同口径：`未结算` / `部分结算（未结 X.00）` / `已结算`（由 `SettlementStateCalculator` 推导）。
  - 比率列输出为**百分比文本**（保留 2 位，如 `25.00%`）：库存余额表「库存占比」= `QuantityRatio × 100`；成本毛利表「毛利率」= `GrossProfitRate × 100`，其值为 `null` 时输出**空单元格**。
  - 标记列按页面文案：库存余额表「成本异常」→ `成本异常` / `-`；成本毛利表「成本完整性」→ `成本不完整` / `-`。
  - **合计行只对可加总列求和**（数量 / 件数 / 金额），比率、均价与标记列留空；首列固定为 `合计`。
  - **汇总类报表「单位」列始终输出且不留空**：xlsx 保持固定列集便于二次处理；**商品维度输出商品单位；「往来单位 / 客户」维度聚合的是多种商品、无单一单位，一律输出占位符 `-`**（合计行同为 `-`），空值占位统一走 `ExportLabels.OrDash`，不允许出现无从辨识的空白单元格。页面行为按 `025` §4.4 不变（该维度不显示单位列）。
- **明细批量取数**：单据类导出的明细一律用仓储的 `GetItemsBy...IdsAsync` 一次取回后按单据分组，**禁止逐单查询**（N+1）；商品编码 / 创建人显示名同理走批量方法。

### 0.3 打印范围与版式

| 打印项 | 打印路由（**顶层路由，不进 `AppLayout`**） | 组件（放该域目录） |
|---|---|---|
| 采购入库单 | `print/purchases/:id` | `PurchaseManagement/PurchasePrintView.vue` |
| 销售出库单 | `print/sales/:id` | `SalesManagement/SalePrintView.vue` |
| 采购退货单 | `print/purchase-returns/:id` | `PurchaseReturnManagement/PurchaseReturnPrintView.vue` |
| 销售退货单 | `print/sales-returns/:id` | `SalesReturnManagement/SalesReturnPrintView.vue` |
| 收付款单 | `print/settlements/:id` | `SettlementManagement/SettlementPrintView.vue` |
| 库存盘点单 | `print/stock-takes/:id` | `StockTakeManagement/StockTakePrintView.vue` |

- 版式（自上而下）：单据标题（如「采购入库单」）→ 单据头 → 明细表 → 合计 → 页脚（打印时间 + 操作人 + 页码）。
- **单据头 / 明细 / 合计的字段按单据类型（2026-09-20 实现期收敛，唯一事实源）**：
  - 单品单据（采购入库 / 销售出库 / 采购退货 / 销售退货）：单据头 = 往来单位 / 单据日期 / 单号 / 备注；明细 = 序号 / 商品名称 / 单位 / 数量 / 单价 / 小计；合计 = 数量合计 + 金额合计。
  - 收付款单：单据头 = 往来单位 / **类型（收款 / 付款）** / 收付日期 / 单号 / 方式 / 备注（不标方向无法辨识收付语义，故类型为必显字段）；明细 = 序号 / 核销单号 / 单据日期 / 单据金额 / 本次核销金额；合计 = 核销总额。
  - 库存盘点单：单据头 = 类型（期初建账 / 库存盘点） / 单据日期 / 单号 / 备注（该单据**无往来单位**，以类型占该位）；明细 = 序号 / 商品编码 / 商品名称 / 单位 / 账面数量 / 实盘数量 / 差异；合计 = 账面数量合计 + 实盘数量合计（无金额列）。
- **打印明细列以实现为准（2026-09-20）**：四类商品明细（入库 / 出库 / 采购退货 / 销售退货）的**详情接口只返回商品名称快照、不含商品编码**，故打印视图明细列不含「商品编码」（不为凑此列新增接口或联查，导出 xlsx 的明细表仍含编码，取 `IProductRepository.GetCodesByIdsAsync`）；盘点明细自带商品编码、收付款为核销明细，各按其详情接口字段展示。
- 交互：页面顶部一条**非打印**工具条（`print-toolbar`，`@media print` 隐藏）含「打印」（`window.print()`）与「返回」（`router.back()`）；打印时间取**打印当刻**（`utils/datetime.ts` 格式化）。
- 数据源：复用该域既有详情接口（`getXxxById`），不新增接口；id 不存在 → `a-result status="404"`。
- 不进侧边菜单、不注册 `MENU_ROUTE_MAP`（`meta: { requiresAuth: true }`，无布局包裹）。

## 1. 总体设计

```
导出（各业务域）
  前端各列表 / 报表「导出」按钮
    → api/request.ts 的 downloadBlob(url, params)（responseType: blob + Content-Type 分流）
      → GET /api/<资源>/export（各域 Controller）
        → Features/<域>/Export<X> → 复用该域既有列表 / 报表查询（取全量）
          → IExcelExporter.Build(ExcelWorkbookModel)（CloseXML 实现，Infrastructure）
            → 二进制流返回

打印（纯前端）
  /print/<域>/:id → <域> PrintView → 该域详情接口 → 打印样式 + window.print()
```

核心原则：

- **导出复用列表查询**：导出用例调用与列表**同一个仓储方法**（只是不做分页、按上限取数），保证「导出 == 所见筛选结果」，不新增查询分支（避免两套筛选口径）。
- **Excel 生成与业务解耦**：`IExcelExporter` 只认识「表格模型」（工作表 / 列 / 行 / 合计行），不认识任何业务类型；各域 Handler 负责把 DTO 映射成表格模型。
- **打印零后端**：不新增接口、不产出 PDF；版式用 `@media print` CSS 控制。
- **两种横向能力都不改既有语义**：不加表、不改写入路径、不改统一响应（除 §0.1 明确的下载例外）。

## 2. 数据模型

> **本规格不新增表 / 实体 / 迁移**。

新增技术组件（`App.Core/Exports/`，非实体）：

| 类型 | 形态 | 说明 |
|---|---|---|
| `ExcelWorkbookModel` | `sealed record` | `Sheets`（工作表集合） |
| `ExcelSheetModel` | `sealed record` | `Name` / `Columns` / `Rows` / `TotalRow?`（合计行） |
| `ExcelColumnModel` | `sealed record` | `Header` / `ValueType` / `Width?` |
| `ExcelValueType` | `enum` | `Text` / `Integer` / `Decimal` / `Date` / `DateTime` |
| `ExportFieldConstraints` | `static class` | `MaxRows = 50000`（导出上限，单一来源） |

## 3. 后端设计

### 3.1 抽象与实现

| 类型 | 位置 | 说明 |
|---|---|---|
| `IExcelExporter` | `App.Core/Abstractions/` | `byte[] Build(ExcelWorkbookModel model)`（同步纯计算，CPU 密集但数据量小） |
| `ClosedXmlExcelExporter` | `App.Infrastructure/Exports/` | `ClosedXML` 实现：Sheet 命名（超 31 字符截断）、首行表头加粗 + 冻结、列宽（给定或按内容估算）、`Decimal` 保留 2 位、`Date` / `DateTime` 格式、合计行加粗 + 顶部细边框；超过 Excel 单表行上限（1048576）时抛 `BusinessException(40000)`（与 `MaxRows` 双保险） |
| 包依赖 | `App.Infrastructure` 追加 `ClosedXML`（最新稳定版，MIT） | 只有 Infrastructure 依赖 Excel 库，Core 不引入 |

- 注册：`AddInfrastructure` 注册 `IExcelExporter → ClosedXmlExcelExporter`（`Singleton`，无状态）。

### 3.2 错误码

> **无新增错误码**。导出上限超限、参数非法均走全局 `40000`（message 说明），服务端异常走 `50000`。

### 3.3 用例与接口

- 接口清单见 §0.2（15 个导出端点，均为各域 Controller 追加的 `GET .../export`）。
- 各导出用例的 `Request` **复用该域既有列表 / 报表 Request**（含其 Validator）；Handler 负责：调仓储取全量（`page = 1`、`pageSize = MaxRows + 1` 用于超限判定）→ 超 `MaxRows` 抛 `40000` → 映射表格模型 → `IExcelExporter.Build` → 返回 `ExportResultDto { FileName, Content(byte[]) }`（Controller 把 `Content` 写为文件流响应）。
- Controller 动作形态（各域一致）：

```csharp
[HttpGet("export")]
public async Task<IActionResult> Export([FromQuery] GetProductsRequest request, CancellationToken ct)
{
    var result = await _mediator.Send(request, ct);   // ExportProductsRequest : GetProductsRequest 语义等价
    return File(result.Content, ExcelContentType, result.FileName);
}
```

- **用例命名与归属**：`Features/<域>/Export<X>`（如 `Products/ExportProducts`），Request 为该域列表 Request 的派生或同构类型（`ExportProductsRequest`，继承 / 复制列表筛选字段并忽略分页），Validator 复用列表校验规则 + 上限规则。

### 3.4 关键用例流程（以 ExportPurchaseOrders 为例）

1. 按列表筛选取**全量**单据（`pageSize = MaxRows + 1`）；超限 → `40000`。
2. 对筛选结果批量取明细（一次查询，按 `OrderId` 分组，避免 N+1）；作废单据照常包含。
3. 组装两个工作表：`单据`（单号 / 供应商 / 日期 / 总金额 / 结算状态 / 单据状态 / 备注 / 创建人 / 创建时间）与 `明细`（单号 / 商品编码 / 商品名称 / 单位 / 数量 / 单价 / 小计）。
4. `IExcelExporter.Build` → 返回 `ExportResultDto`（文件名 `采购入库_202609171030.xlsx`）。

### 3.5 校验规则（FluentValidation，仅格式层）

| 请求 | 规则 |
|---|---|
| 各 `Export<X>Request` | 与该域列表 Request 的筛选规则**完全一致**（复用校验器或继承）；额外不接受 `page` / `pageSize`（若前端传入则忽略，不校验） |

- 导出上限（`MaxRows`）与 Excel 单表上限属业务判定，在 Handler 内（后端规则 §4.1）。

### 3.6 Swagger

- **不分组**（同既有约定）：15 个导出接口按现有方式出现在单文档 Swagger 中，`[ProducesResponseType(200, Type = typeof(FileResult))]` 标注。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   ├── request.ts                      # 追加 downloadBlob(url, params)（统一解包分流的唯一实现）
│   ├── export.ts                       # 各域导出函数（按域聚合，见下）
│   └── report.ts                       # 追加 5 个报表导出函数
└── views/
    ├── PurchaseManagement/PurchasePrintView.vue          # 打印视图（新增，域内平铺）
    ├── SalesManagement/SalePrintView.vue
    ├── PurchaseReturnManagement/PurchaseReturnPrintView.vue
    ├── SalesReturnManagement/SalesReturnPrintView.vue
    ├── SettlementManagement/SettlementPrintView.vue
    └── StockTakeManagement/StockTakePrintView.vue
```

- 接口归属决策（前端规则 §3）：**导出函数集中在 `api/export.ts`**（横向能力，被所有域页面消费；放各域文件会导致 15 个域文件互相重复同一套 blob 处理），报表导出并入 `api/report.ts`（同域页面）；`downloadBlob` 作为唯一实现放 `api/request.ts`（与统一解包同处，避免两套响应处理）。

### 4.2 路由

| path | name | 组件 | 说明 |
|---|---|---|---|
| `print/purchases/:id` | `purchasePrint` | `PurchasePrintView` | 顶层路由，**不进 `AppLayout`** |
| `print/sales/:id` | `salePrint` | `SalePrintView` | 同上 |
| `print/purchase-returns/:id` | `purchaseReturnPrint` | `PurchaseReturnPrintView` | 同上 |
| `print/sales-returns/:id` | `saleReturnPrint` | `SalesReturnPrintView` | 同上 |
| `print/settlements/:id` | `settlementPrint` | `SettlementPrintView` | 同上 |
| `print/stock-takes/:id` | `stockTakePrint` | `StockTakePrintView` | 同上 |

- 均为懒加载 + `meta: { requiresAuth: true }`；不进侧边菜单、不进 `MENU_ROUTE_MAP`。

### 4.3 页面交互

**导出按钮**（各列表操作行右组「数据操作」，位置与分组遵循 `specs/006-list-showcase/design.md` §0；图标 `IconDownload`，颜色默认）：

- 点击 → `exporting` 置位 → `downloadBlob(域导出端点, 当前已应用筛选)` → `try/finally` 复位；防重入。
- 报表页导出含合计行（后端保证）；导出按钮在 `025` / `026` 页面已预留（本规格移除 `:disabled`）。
- 暂无数据的导出：后端仍产出「只有表头」的 xlsx（不报错），前端提示 `Message.info('已导出空数据模板')`（决策见 §5）。

**打印入口**（单据详情页与列表操作列）：

- 详情页：`a-page-header` 头部操作区追加「打印」（`IconPrinter`，默认色）；列表操作列：作为可收纳操作（`011` §0 平铺顺序与阈值规则），`router.push({ name: 'xxxPrint', params: { id } })`。
- 打印视图：`print-toolbar`（非打印）+ 单据版式区（`print-page`）；`@media print` 隐藏工具条、去背景色、`@page { size: A4 portrait; margin: 12mm; }`；分页靠表格行自然分页，明细行 `break-inside: avoid`。

### 4.4 按钮 loading（遵循 `specs/010-button-loading/design.md` §0）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 导出 | `exporting` | 导出按钮（+ 防重入） |
| 打印 | 不置 loading | `window.print()` 为同步动作 |
| 打印视图数据加载 | `loading` | 页面级 `a-spin`（非按钮） |

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| 导出在后端 | `ClosedXML` + 文件流 | 列表是服务端分页，前端导只能导当前页；后端导出即「筛选全量」，语义正确且数据量可控 |
| 文件下载例外 | 成功返回二进制流 | 无法在统一响应里承载文件内容；base64 包装会膨胀 33% 且占内存；例外条款写进 §0.1 并在总则登记 |
| 导出用例归属各域 | `Features/<域>/Export<X>` | 导出是「该域列表的另一种出参」，不是独立域；复用该域 Request / 仓储 / 筛选口径，避免第二套筛选逻辑 |
| Excel 库选 ClosedXML | MIT、API 简洁 | EPPlus 5+ 为商业许可；NPOI 冗长；ClosedXML 足够满足表格导出 |
| 表格模型解耦 | `IExcelExporter` 只认表格 | Core 不依赖 Excel 库；各域只负责「DTO → 表格模型」，可单测映射 |
| 列集 = 列表列定义 | 去序号 / 操作列 + 创建信息 | 导出即「列表所见」，用户无需学习第二套列；各域列定义仍是唯一事实源（本规格不复制） |
| 导出上限 5 万行 | `ExportFieldConstraints.MaxRows` | 防单次导出拖垮内存 / 请求超时；超限提示缩小筛选范围（而不是静默截断） |
| 空数据仍产出模板 | 只有表头的 xlsx | 交付 / 归档场景需要固定模板；静默失败反而困惑 |
| 打印在前端 | `window.print()` + `@media print` | 零后端依赖、版式可控、用户可另存 PDF；服务端 PDF 需排版引擎与字体，收益低 |
| 打印路由脱离布局 | 顶层路由 | 打印必须无侧边栏 / 工具条；放 `AppLayout` 内需额外 CSS 隐藏，易漏项 |
| 分类不做导出 | 不做 | 单级字典，页面内可维护，导出无交付价值 |
| 无 RBAC（`028` 前） | 登录即可导出 / 打印 | 同既有功能；`028` 落地时导出纳入 `<域>.export` 权限点（§0.2 端点已在 `028` 权限点清单中登记） |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储接口与 `IExcelExporter`；时间用固定 `DateTimeOffset` 入参或注入时钟，不读 `DateTime.Now`。

- **各导出用例**（15 个，按域覆盖关键项，避免重复断言同一逻辑）：
  - 筛选传参：断言调用既有仓储方法时 `page = 1`、`pageSize = MaxRows + 1`、筛选字段透传（与列表一致）。
  - 超限：仓储返回 `MaxRows + 1` 行 → `40000`（message 含上限）；恰好 `MaxRows` 行 → 通过。
  - 表格模型映射：列头顺序与 §0.2 规则一致（去序号 / 操作列、含创建信息）；单据类产出 2 个工作表且明细首列为单号；报表类含合计行。
  - 空结果：产出仅表头的模型（不抛错）。
- **`ClosedXmlExcelExporter`**：给定多工作表 / 合计行 / 各 `ExcelValueType` 的模型 → 产物可被 ClosedXML 重新打开（round-trip 断言）；Sheet 名超长截断；金额 2 位；`MaxRows` 超 Excel 单表上限 → `40000`。
- **Controller 响应**：导出动作返回 `FileResult` 且 Content-Type 为 xlsx 类型（集成测试断言）。
- **错误回退**：参数非法时返回统一响应 JSON（集成测试：`/api/products/export?pageSize=1000` → JSON 且 `code = 40000`）。
