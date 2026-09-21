---
created: 2026-09-16
updated: 2026-09-21
---

# 任务清单：收付款与应收应付（erp-settlement）

> 依据 `specs/023-erp-settlement/design.md` 拆分。含**既有单据结算语义改造**（四张单据表 + 四类单据前后端）+ 收付款单 + 往来对账。按顺序实现，完成后勾选。
> 前置：`015` / `016` / `021` / `022` 已实现（本规格改造其结算字段、接口与页面）；建议在 `021` / `022` 之后实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [x] 1.1 新增 `App.Core/Entities/Settlement.cs`、`SettlementItem.cs` 与枚举 `SettlementType` / `SettlementMethod` / `SettlementOrderType`
- [x] 1.2 `Persistence/Configurations/SettlementConfiguration.cs`、`SettlementItemConfiguration.cs` + `AppDbContext` 新增 2 个 `DbSet`
- [x] 1.3 四张单据表改造：`SettlementStatus` → `SettledAmount`（实体 + EF 配置同步，`numeric(18,2)` 默认 0）
- [x] 1.4 增量迁移 `dotnet ef migrations add AddErpSettlement -p src/App.Infrastructure -s src/App.Api`，并在迁移内回填历史数据（`SettlementStatus = 1` → `SettledAmount = TotalAmount`）
- [x] 1.5 删除 `OrderSettlementStatus` 枚举并清理全部引用

## 二、后端：仓储与用例

- [x] 2.1 新增 `App.Core/Abstractions/ISettlementRepository.cs` + `App.Infrastructure/Repositories/SettlementRepository.cs`（`AddAsync` / `GetPagedAsync` / `GetDetailAsync` / `UpdateStatusAsync` / `GenerateSettlementNoAsync`）
- [x] 2.2 新增 `App.Core/Abstractions/ISettlementQueryRepository.cs` + 读模型 `SettlementCandidateItem` / `ReconciliationItem` + 实现（未结候选 / 往来台账）
- [x] 2.3 四类单据仓储：移除 `UpdateSettlementAsync`、新增 `AddSettledAmountAsync`（`ExecuteUpdateAsync` 原子累加）；列表 `GetPagedAsync` 的 `settlement` 参数改为按 `settlementState` 过滤
- [x] 2.4 共享出参 `Features/Settlements/SettlementListItemDto.cs` / `SettlementDetailDto.cs` / `SettlementCandidateDto.cs` / `ReconciliationListItemDto.cs` + `SettlementsDtoMapper.cs`
- [x] 2.5 新增用例 `Settlements/GetSettlements`
- [x] 2.6 新增用例 `Settlements/CreateSettlement`（方向 / 往来 / 未结金额校验 → 落单 + 逐行累加 `SettledAmount`；同一事务）
- [x] 2.7 新增用例 `Settlements/GetSettlementById`
- [x] 2.8 新增用例 `Settlements/VoidSettlement`（逐行回退 `SettledAmount`）
- [x] 2.9 新增用例 `Settlements/GetUnsettledOrders`（按方向映射可核销单据类型集合）
- [x] 2.10 新增用例 `Settlements/GetReconciliation`（往来台账）
- [x] 2.11 新增 `App.Api/Controllers/SettlementsController.cs`（6 端点；`unsettled-orders` 注册在 `{id:guid}` 之前）
- [x] 2.12 移除四类单据的 `UpdateXxxSettlement` 用例、Controller 端点与 `DependencyInjection` 注册
- [x] 2.13 四类单据 DTO 改造：`settlementStatus` → `settledAmount` + `unsettledAmount` + `settlementState`（`Mapper` 内推导）；列表请求参数与筛选同步
- [x] 2.14 `App.Core` / `App.Infrastructure` `DependencyInjection.cs` 注册新用例、Validator 与两个新仓储

## 三、单元测试

- [x] 3.1 `CreateSettlement` 成功：四种核销组合（收款 × 销售单 / 采购退货，付款 × 采购单 / 销售退货）各一例，断言单号前缀、总额重算、逐行累加、明细快照、`Commit`
- [x] 3.2 `CreateSettlement` 异常：`40110` / `40112`（含单号与未结金额）/ `40113` / `40114` / `40400` / `40104` / `40108` / `40109`；失败路径无任何金额累加 + `RollbackAsync` 断言
- [x] 3.3 `VoidSettlement`（回退累加 + 已作废 `40104` / 不存在 `40400`）、`GetSettlements` / `GetSettlementById`（筛选 / 分页 / 快照）
- [x] 3.4 `GetUnsettledOrders` 方向 → 单据类型映射；`GetReconciliation` 应收 / 应付聚合（含退货冲减与已收 / 已付抵扣）与未结单据数
- [x] 3.5 单据侧回归：四类单据 `SettlementState` 推导四态（0 / 部分 / 相等 / 超额）与 `settlementState` 筛选传参；**删除**四个 `UpdateXxxSettlement` 测试
- [x] 3.6 Validator 边界 + `FieldValidationConsistencyTests` 扩展（`SettlementNo` 列长、`amount` 边界与精度、items 上限、方向枚举）
- [x] 3.7 `cd backend && dotnet build` / `dotnet test` 通过（既有用例全部回归）

## 四、前端

- [x] 4.1 `src/api/settlement.ts`（类型 + `getSettlements` / `createSettlement` / `getSettlementById` / `voidSettlement` / `getUnsettledOrders` / `getReconciliation`）
- [x] 4.2 `src/views/SettlementManagement/SettlementsView.vue`（筛选 + 表格 + 作废）
- [x] 4.3 `SettlementFormPage.vue`（类型 / 往来 / 日期 / 方式 + 可核销单据子表格 + 未结金额默认填充 + 「全部结清」+ `submitting`）
- [x] 4.4 `SettlementDetailView.vue`（表头 + 核销明细只读 + 作废 + 404 空态）
- [x] 4.5 `ReconciliationView.vue`（往来余额表格 + 未结单据抽屉）
- [x] 4.6 `src/router/index.ts` 新增 `settlements` / `settlements/new` / `settlements/detail/:id` / `reconciliation`
- [x] 4.7 `src/components/AppLayout.vue`「进销存」分组追加「收付款」「往来对账」+ `MENU_ROUTE_MAP` 增加 `settlementDetail`
- [x] 4.8 单据四域前端改造：移除结算切换按钮与 `settlingId`，改为结算状态标签 + 未结金额，操作列新增「收付款」跳转（预置方向与往来单位），筛选「结算状态」下拉改为 未结算 / 部分结算 / 已结算
- [x] 4.9 `src/api/purchase.ts` / `sale.ts` / `purchaseReturn.ts` / `saleReturn.ts`：移除 `updateXxxSettlement`，类型同步改造
- [x] 4.10 结算状态标签文案与颜色映射抽到公共常量（`src/utils/settlement.ts`），供单据页与收付款页共用
- [x] 4.11 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 五、E2E（Playwright）

- [x] 5.1 新增 `e2e/settlement.spec.ts`：部分收款 → 销售单显示「部分结算（未结 x）」；收满 → 「已结算」；收付款单详情核销明细正确
- [x] 5.2 同文件：作废收付款单 → 单据金额与状态回退；方向不匹配 / 超未结金额被拒（错误提示可见）
- [x] 5.3 同文件：往来对账页应收 / 应付余额随收款变化，未结单据抽屉内容正确
- [x] 5.4 改造既有 spec：`purchase.spec.ts` / `sale.spec.ts` / `purchase-return.spec.ts` / `sale-return.spec.ts` 中结算相关断言（移除手工切换用例，改为金额 / 状态标签 + 「去收付款」跳转断言）
- [x] 5.5 `cd frontend && npm run test:e2e` 全量通过

## 六、规格与上下文联动

- [x] 6.1 `specs/015-erp-purchase` / `016-erp-sale` / `021-erp-purchase-return` / `022-erp-sale-return` 的 `design.md` 加「演进（erp-settlement）」注记：结算由状态位升级为已结算金额、手工切换端点与按钮移除、列表筛选参数变更
- [x] 6.2 `specs/003-api-swagger/design.md` 端点清单（若有相关断言）与 `tests/App.Tests` 中的端点断言同步
- [x] 6.3 `.codebuddy/CONTEXT.md` §2（新 Feature / 仓储 / 读模型 / 错误码）、§3（新前端域目录与 api 文件）、§6（规格清单分类）同步
- [x] 6.4 `specs/ROADMAP.md` 状态列更新（`023` → 已实现）

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。

## 七、变更（2026-09-20）：单据结算明细反查（详情页「收付款明细」）

> 需求 / 设计见 `requirement.md` F3 / F5 / 验收标准 7，`design.md` §3.1 / §3.3 / §3.4 / §3.5 / §4.2 / §4.4 / §5 / §6。

- [x] 7.1 后端：`GetSettlementsRequest` 增可选 `OrderType` + `OrderId`；Validator 校验取值合法且**成对出现**
- [x] 7.2 后端：`ISettlementRepository.GetPagedAsync` / 实现增 `orderType` + `orderId`（按核销明细反查，含已作废）
- [x] 7.3 后端：`SettlementListItemDto` 增 `OrderAmount`；Mapper 支持传入；`GetSettlements` Handler 按单据反查时用既有 `GetItemsBySettlementIdsAsync` 补齐本单核销金额
- [x] 7.4 后端测试：Handler 透传 / `orderAmount` 计算 / 未传 `orderId` 不查询；`SettlementRepositoryTests` 按单过滤（命中返回、未命中排除）
- [x] 7.5 前端：`api/settlement.ts` 的 `SettlementQuery` 增 `orderType` / `orderId`，`SettlementListItem` 增 `orderAmount`
- [x] 7.6 前端：新增跨域共享组件 `components/SettlementRecords.vue`（只读 + 跳详情 + 作废行置灰 + `loading`）
- [x] 7.7 前端：四类单据详情（采购入库 / 销售出库 / 采购退货 / 销售退货）接入「收付款明细」区块
- [x] 7.8 E2E：`settlement.spec.ts` 补「销售出库详情可见该笔收付款单并可跳详情」
- [x] 7.9 验证：`dotnet test`（540 通过）与 `npm run e2e:run`（121 通过）通过
- [x] 7.10 前端：单据类型文案与详情路由收敛到 `utils/settlement.ts`（`settlementOrderTypeLabel` / `settlementOrderTypeRouteName`）；`SettlementRecords.vue`、`SettlementDetailView.vue`（核销明细）、`ReconciliationView.vue`（未结单据抽屉）的**单号改 `a-link` 超链接**并去掉「操作」列；`PurchaseDetailView` / `SaleDetailView` 的「关联订单」同样改超链接
- [x] 7.11 E2E：`settlement.spec.ts` / `purchase-order.spec.ts` / `sale-order.spec.ts` 改为断言「单号链接存在 / 点击跳转」，并覆盖关联订单超链接回跳

## 八、变更（2026-09-21）：已核销单据禁止作废

> 需求 / 设计见 `requirement.md` 目标 6 / F7 / 验收标准 9，`design.md` §0 / §1 / §3.2 / §3.6 / §5 / §6。

- [x] 8.1 后端：`App.Core/Errors/ErrorCode.cs` 追加 `40120 OrderSettledCannotVoid`
- [x] 8.2 后端：四类单据作废用例（`VoidPurchaseReceipt` / `VoidSalesShipment` / `VoidPurchaseReturn` / `VoidSalesReturn`）在「已作废 `40104`」校验后追加「已核销 `40120`」校验（`SettledAmount > 0` 直接拒绝，不开事务）
- [x] 8.3 后端测试：四类单据各补「已核销 → `40120` 且未开事务 / 未回冲 / 不写流水 / 状态不变」用例；`cd backend && dotnet build` / `dotnet test` 全绿（544 通过）
- [x] 8.4 E2E：`settlement.spec.ts` 补「销售单收款后作废被拒（提示可见）→ 作废收款单回退 → 再作废成功」；`npm run test:e2e` 全量通过（122 通过）
- [x] 8.5 规格联动：`specs/015` / `016` / `021` / `022` / `024` 的 `design.md` 加「演进（erp-settlement，已核销禁作废）」注记；`.codebuddy/CONTEXT.md` 错误码同步
