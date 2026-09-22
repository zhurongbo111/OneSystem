---
created: 2026-09-17
updated: 2026-09-22
---

# 任务清单：发票登记（erp-invoice）

> 依据 `specs/032-erp-invoice/design.md` 拆分。含后端发票域（主表 + 关联明细 + 只读候选查询）+ 5 个用例 + 前端列表 / 新建 / 详情。按顺序实现，完成后勾选。
> 前置：`023`（结算与往来、`SettlementOrderType` 枚举、`ISettlementQueryRepository` 模式）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与仓储

- [x] 1.1 新增枚举 `InvoiceType`；实体 `Invoice` / `InvoiceItem`；`InvoiceFieldConstraints`
- [x] 1.2 EF 配置 `InvoiceConfiguration` / `InvoiceItemConfiguration`（`InvoiceNo` 唯一、明细 `(OrderType, OrderId)` 复合索引）+ `AppDbContext` 2 个 `DbSet`
- [x] 1.3 增量迁移 `dotnet ef migrations add AddErpInvoice -p src/App.Infrastructure -s src/App.Api`
- [x] 1.4 读模型 `InvoicableOrderItem` / `InvoiceListItem`（含 `OrderNoSummary`、`TaxRate`）
- [x] 1.5 `IInvoiceRepository` + 实现（`AddAsync` / `GetPagedAsync`（含关联单据号检索）/ `GetDetailAsync` / `ExistsByInvoiceNoAsync` / `UpdateStatusAsync` / `GetItemsByInvoiceIdsAsync`）+ 注册
- [x] 1.6 `IInvoiceQueryRepository` + 实现（`GetInvoicableAsync` 四表合并分页 / `GetInvoicedAmountAsync` 聚合）+ 注册
- [x] 1.7 `ErrorCode.cs` 追加 `40132` / `40133` / `40134` / `40135`

## 二、后端：用例与接口

- [x] 2.1 共享出参 `Features/Invoices/InvoiceListItemDto.cs` / `InvoiceDetailDto.cs` / `InvoicableOrderDto.cs` + `InvoicesDtoMapper.cs`
- [x] 2.2 新增用例 `Invoices/GetInvoices`、`CreateInvoice`（方向 / 往来 / 作废 / 未开票金额校验 → 重算税额与合计 → 落单；同一事务）
- [x] 2.3 新增用例 `Invoices/GetInvoiceById`、`VoidInvoice`、`GetInvoicableOrders`
- [x] 2.4 `InvoicesController`（5 端点 + 导出，`invoicable-orders` / `export` 在 `{id:guid}` 之前）+ DI 注册
- [x] 2.5 `029` 审计接入：发票创建 / 作废（摘要含发票号、往来、金额、关联单号）

## 三、单元测试

- [x] 3.1 `CreateInvoice` 成功：税额 / 合计后端重算、明细快照、`Commit`、审计参数
- [x] 3.2 `CreateInvoice` 异常：`40110` / `40132` / `40400` / `40108` / `40109` / `40104` / `40134` / `40135` / `40133`（含单号与未开票金额）；失败路径无写入 + 事务内写入失败回滚
- [x] 3.3 `VoidInvoice`（仅改主表状态；已作废 `40104` / 不存在 `40400`）、`GetInvoices` / `GetInvoiceById`（筛选含单据号 / 分页 / 快照 / `40400`）
- [x] 3.4 `GetInvoicableOrders` 方向映射与过滤透传
- [x] 3.5 金额口径：税额舍入（含 `0.005` 与 `4.3329` 边界）、`TaxRate` 0 与 1、明细金额上限
- [x] 3.6 未开票金额恒等：`1000 → 100 → 0 →（作废）900` 链路
- [x] 3.7 字段约束一致性（`InvoiceNo` 50 / `TaxRate` 精度 / `OrderNo` 20 / `keyword` 50-51）
- [x] 3.8 `cd backend && dotnet build` / `dotnet test` 通过（既有用例全部回归）

## 四、前端

- [x] 4.1 `src/api/invoice.ts`（5 个接口 + 类型 + 类型 / 状态标签映射常量）
- [x] 4.2 `views/InvoiceManagement/InvoicesView.vue`（筛选 + 类型标签 + 作废 + 分页）
- [x] 4.3 `InvoiceFormPage.vue`（表头 + 税额 / 合计实时计算 + 可开票单据子表格 + 金额一致性提示 + `submitting`）
- [x] 4.4 `InvoiceDetailView.vue`（表头 + 关联单据只读 + 作废 + 404 空态）
- [x] 4.5 `router/index.ts` 新增 3 条路由；`AppLayout.vue`「资金」分组追加「发票登记」+ `MENU_ROUTE_MAP` 增加 `invoiceDetail`
- [x] 4.6 `027` 导出范围表续行（发票列表导出，2 工作表：发票 + 关联明细）
- [x] 4.7 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 五、E2E（Playwright）

- [x] 5.1 新增 `e2e/invoice.spec.ts`：登记销项发票（关联 2 张销售单，部分开票）→ 详情展示正确（含税额 / 合计）
- [x] 5.2 同文件：同一单据再次开票超未开票金额被拒（提示可见）；作废后该单据可再次开票
- [x] 5.3 同文件：销项票关联采购单被拒（`40134`）；发票号重复被拒（`40132`）
- [x] 5.4 同文件：列表筛选（类型 / 往来 / 日期 / 单据号关键词）与作废行置灰
- [x] 5.5 `cd frontend && npm run test:e2e` 全量通过（含 `sale` / `purchase` / `settlement` 既有用例回归）

## 六、规格与上下文联动

- [x] 6.1 `specs/023-erp-settlement/design.md` 加注记：`SettlementOrderType` 语义泛化为「可关联单据类型」（`032` 复用）；若实施时改名 `BusinessOrderType`，两规格同步
- [x] 6.2 `specs/036-erp-partner-price/design.md` 加注记：发票与信用额度无直接关系（额度看应收，不看开票）
- [x] 6.3 `specs/028-erp-rbac/design.md` §0.2 续行 `invoices.export`（导出权限点；原表仅登记 view / create / void）
- [x] 6.4 `specs/029-erp-audit-log/design.md` §0.1 续行：发票登记 / 作废（摘要模板按实现收敛）
- [x] 6.5 `.codebuddy/CONTEXT.md` §2（Invoices 实体 / 枚举 / 仓储 / 错误码）、§3（InvoiceManagement 域、api 文件）、§6 同步
- [x] 6.6 `specs/ROADMAP.md` 状态列更新（`032` → 已实现）；§5「范围外」注记开票接口与税控由本规格确认不做

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 发票与收付款互不影响（同一单据可同时有核销与开票），未开票金额与未结金额两个口径互不推导（单测断言）。
