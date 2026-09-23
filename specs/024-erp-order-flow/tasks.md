---
created: 2026-09-16
updated: 2026-09-23
---

# 任务清单：两段式单据（erp-order-flow）

> 依据 `specs/024-erp-order-flow/design.md` 拆分。含**既有单据域重命名（大范围重构，行为不变）** + 订单层新增 + 关联回写 + 前端订单页与开单页改造。**按阶段顺序实现，每阶段结束跑全量测试**。
> 前置：`019`（流水）已实现；`021` / `022`（退货）与 `023`（结算）建议先落地（本规格会重命名它们消费的单据表与接口路径）。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 零、前置门禁（未确认不得开工）

- [x] 0.1 **用户确认**（2026-09-17 已确认，按 `design.md` §5 推荐路径执行）：「既有单据表重命名（`PurchaseOrders` → `PurchaseReceipts`、`SalesOrders` → `SalesShipments`）」与「历史单号前缀改写（`PO`→`GR`、`SO`→`GI`）」**两项均做**；确认后进入阶段一

## 一、阶段 A：重命名既有单据域（重构，行为与 UI 不变）

- [x] 1.1 后端实体 / 表重命名：`PurchaseOrder(s)` → `PurchaseReceipt(s)`、`SalesOrder(s)` → `SalesShipment(s)`；明细 `OrderId` → `ReceiptId` / `ShipmentId`；主表 `OrderNo` → `ReceiptNo` / `ShipmentNo`
- [x] 1.2 后端用例目录重命名：`Features/Purchases` → `Features/PurchaseReceipts`、`Features/Sales` → `Features/SalesShipments`（含命名空间、DTO / Mapper 名称）
- [x] 1.3 仓储重命名：`IPurchaseOrderRepository` → `IPurchaseReceiptRepository`、`ISalesOrderRepository` → `ISalesShipmentRepository`（含实现与 DI 注册）
- [x] 1.4 Controller 与路由重命名：`PurchaseOrdersController` → `PurchaseReceiptsController`（`/api/purchase-receipts`）、`SalesOrdersController` → `SalesShipmentsController`（`/api/sales-shipments`）
- [x] 1.5 单号前缀参数改为 `GR` / `GI`；`OrderFieldConstraints` 常量引用不变
- [x] 1.6 增量迁移 `AddErpRenameReceiptsShipments`：`RenameTable` / `RenameColumn` + 历史单号前缀改写（`migrationBuilder.Sql`：`'GR' || substring(...)` / `'GI' || substring(...)`）
- [x] 1.7 后端测试重命名与跑通（既有断言改为新名称 / 新路由；`dotnet test` 全绿）
- [x] 1.8 前端 `api/purchase.ts` / `api/sale.ts` 改为对接新路由；页面文案「销售开单」→「销售出库」；路由 `purchases` / `sales` **保持不变**
- [x] 1.9 e2e 既有 `purchase.spec.ts` / `sale.spec.ts`（及退货 / 结算相关用例）适配新路由与文案，`npm run test:e2e` 全量通过（**此阶段结束必须是「行为零变化」的绿色状态**）

## 二、阶段 B：订单层（采购 / 销售订单）

- [x] 2.1 新增枚举 `OrderFlowStatus`（`Voided` / `Pending` / `Partial` / `Completed` / `Closed`）
- [x] 2.2 新增实体 `PurchaseOrder` / `PurchaseOrderItem`（含 `FulfilledQuantity`）+ EF 配置 + `DbSet`
- [x] 2.3 新增实体 `SalesOrder` / `SalesOrderItem` + EF 配置 + `DbSet`
- [x] 2.4 新增 `IPurchaseOrderRepository`（订单语义：`GetPagedAsync` / `GetDetailAsync` / `AddAsync` / `UpdateAsync` / `UpdateFlowStatusAsync` / `GetLinesAsync` / `GetPicksAsync` / `AddFulfilledQuantityAsync` / `GenerateOrderNoAsync`）+ 实现；销售侧同构
- [x] 2.5 增量迁移 `AddErpOrders`（订单 4 张表 + 出入库单新增 `OrderId` / `OrderNo` / `OrderItemId` 列）
- [x] 2.6 错误码追加 `40115` / `40116` / `40117`
- [x] 2.7 采购订单 6 用例（`GetPurchaseOrders` / `CreatePurchaseOrder` / `GetPurchaseOrderById` / `UpdatePurchaseOrder` / `VoidPurchaseOrder` / `ClosePurchaseOrder`）+ 共享出参 + Mapper
- [x] 2.8 销售订单 6 用例 + 共享出参 + Mapper
- [x] 2.9 `PurchaseOrdersController` / `SalesOrdersController`（订单端点）；订单侧**不触碰**库存与流水
- [x] 2.10 DI 注册（12 个 Handler + Validator）
- [x] 2.11 单元测试：订单 12 个用例的成功 / 状态限制 / 异常分支；断言订单不写库存与流水（含 `OrderFlowFieldConsistencyTests` 字段约束守护）

## 三、阶段 C：出入库单关联订单

- [x] 3.1 `CreatePurchaseReceipt` 改造：`orderId` / `orderItemId` 校验（`40400` / `40104` / `40116` / `40115` / `40117`）→ 插单 + 库存 + 流水 → `AddFulfilledQuantityAsync(+q)` → 订单状态推导与更新（同一事务）
- [x] 3.2 `VoidPurchaseReceipt` 改造：库存回冲 + 流水 → `AddFulfilledQuantityAsync(-q)` → 状态重算（`Closed` 不回退）
- [x] 3.3 销售侧 `CreateSalesShipment` / `VoidSalesShipment` 同构改造（库存先扣 / 回增，`40103` 语义不变）
- [x] 3.4 新增只读用例：`GetPurchaseOrderPicks`（候选订单：`Pending` / `Partial`，按供应商）与 `GetPurchaseOrderLines`（订单明细 + 未收数量）；销售侧同构；Controller 固定段路由置于 `{id:guid}` 之前
- [x] 3.5 出入库单列表 / 详情出参与筛选新增 `orderId` / `orderNo`
- [x] 3.6 单元测试：关联成功（累计量 + 状态三态）/ 超量 `40115` / 状态不允许 `40116` / 供应商不一致 `40117` / 明细不属于订单 `40400` / 作废回退与 `Closed` 不回退 / 不关联订单路径不调用订单仓储
- [x] 3.7 `IPurchaseReceiptRepository` / `ISalesShipmentRepository` 的 `GetPagedAsync` 返回 `(单据 Order, int TotalQuantity)`（本页单据明细数量一次 `GroupBy` 聚合）
- [x] 3.8 入库单 / 出库单列表行与详情出参新增 `totalQuantity`（列表取仓储聚合值、详情 Mapper 内按明细求和）；`GetPurchaseReceipts` / `GetSalesShipments` Handler 与两个导出 Handler 适配
- [x] 3.9 单元测试：列表 Handler 聚合数量透传断言（`GetPurchaseReceiptsRequestHandlerTests` / `GetSalesShipmentsRequestHandlerTests`）；受影响的既有假实现与导出用例适配
- [x] 3.10 `cd backend && dotnet build` / `dotnet test` 全绿（含阶段 A 回归）

## 四、阶段 D：前端

- [x] 4.1 `src/api/purchaseOrder.ts` / `saleOrder.ts`（订单 CRUD + 作废 / 关闭 + 候选订单 + 订单明细）
- [x] 4.2 `PurchaseOrdersView.vue`（筛选 / 状态标签 / 未收数量列 / 操作列按 `specs/011-action-column` §0 收纳）
- [x] 4.3 `PurchaseOrderFormPage.vue`（新建 / 编辑共用，`mode` 区分，含明细子表格 + `submitting`）
- [x] 4.4 `PurchaseOrderDetailView.vue`（表头 + 明细含已收 / 未收 + 关联入库单列表 + 编辑 / 关闭 / 作废 / 去入库）
- [x] 4.5 `SalesOrderManagement/` 三页同构（客户 / 销售价 / 去出库）
- [x] 4.6 `PurchaseFormPage.vue` 改造：关联订单下拉（`pick-orders`）+ 订单明细带出（`order-lines`）+ 数量上限 / 只读单价 + `orderLinesLoading`
- [x] 4.7 `SaleFormPage.vue` 同构改造（保留库存预警）
- [x] 4.8 订单号展示：入库 / 出库单列表与详情新增订单号列 / 描述项
- [x] 4.9 `src/router/index.ts` 新增订单 8 条路由（`purchase-orders` / `new` / `edit/:id` / `detail/:id` 与销售同构）
- [x] 4.10 `src/components/AppLayout.vue` 追加「采购订单」「销售订单」菜单项 + `MENU_ROUTE_MAP` 映射
- [x] 4.11 `api/purchase.ts` / `api/sale.ts` 列表行类型新增 `totalQuantity`；订单详情的关联入库单 / 出库单列表新增「数量合计」列，**单号改为 `a-link` 超链接**直达详情（`/purchases/detail/:id` / `/sales/detail/:id`），不设「操作」列与「详情」按钮
- [x] 4.12 入库 / 出库单详情「关联订单」改为 `a-link` 超链接（→ 采购 / 销售订单详情，无关联时显示 `—`）
- [x] 4.13 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 五、阶段 E：E2E（Playwright）

- [x] 5.1 新增 `e2e/purchase-order.spec.ts`：下单 → 查看在途量 → 部分入库（关联订单）→ 订单「部分收货」→ 再入库剩余 → 「已完成」
- [x] 5.2 同文件：关联入库超量被拒；订单在部分收货后编辑 / 作废被拒（提示可见）；关闭订单后可关闭状态展示
- [x] 5.3 同文件：入库单作废 → 订单状态与未收数量回退
- [x] 5.4 新增 `e2e/sale-order.spec.ts`：同构链路（待发货 / 部分发货 / 已完成）
- [x] 5.5 改造既有 `purchase.spec.ts` / `sale.spec.ts`：新增「不关联订单直接入库」回归用例；文案与路由适配
- [x] 5.6 `purchase-order.spec.ts` / `sale-order.spec.ts` 补「关联单据数量合计正确 + 单号超链接跳转」
- [x] 5.7 订单 ↔ 单据详情之间的「单号 / 关联订单」超链接跳转与回跳断言
- [x] 5.8 `cd frontend && npm run test:e2e` 全量通过

## 六、阶段 F：规格与上下文联动

- [x] 6.1 `specs/015-erp-purchase` / `016-erp-sale` `design.md` 加「演进（erp-order-flow）」注记：表 / 接口路径 / 单号前缀重命名，新增 `OrderId` 关联，明细 `OrderId` → `ReceiptId` / `ShipmentId`，列表返回形状新增 `totalQuantity`
- [x] 6.2 `specs/021` / `022` / `023` `design.md` 同步重命名后的表名与接口路径（其消费的单据表已改名）
- [x] 6.3 `specs/003-api-swagger` 端点清单与断言同步（该规格只断言 `login` / `me` / `health` 三个基础端点，未枚举业务端点，核对后无需改动）
- [x] 6.4 `.codebuddy/CONTEXT.md` §2（Feature / 仓储重命名与新域）、§3（新前端域目录与 api 文件）、§6（规格清单）同步
- [x] 6.5 `specs/ROADMAP.md` 状态列更新（`024` → 已实现）与本文件 §0 门禁结果记录

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 阶段 A 结束时必须满足「行为零变化」：不含订单功能的前提下，既有功能测试全绿。
