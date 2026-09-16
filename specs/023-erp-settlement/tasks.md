---
created: 2026-09-16
updated: 2026-09-16
---

# 任务清单：收付款与应收应付（erp-settlement）

> 依据 `specs/023-erp-settlement/design.md` 拆分。含**既有单据结算语义改造**（四张单据表 + 四类单据前后端）+ 收付款单 + 往来对账。按顺序实现，完成后勾选。
> 前置：`015` / `016` / `021` / `022` 已实现（本规格改造其结算字段、接口与页面）；建议在 `021` / `022` 之后实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [ ] 1.1 新增 `App.Core/Entities/Settlement.cs`、`SettlementItem.cs` 与枚举 `SettlementType` / `SettlementMethod` / `SettlementOrderType`
- [ ] 1.2 `Persistence/Configurations/SettlementConfiguration.cs`、`SettlementItemConfiguration.cs` + `AppDbContext` 新增 2 个 `DbSet`
- [ ] 1.3 四张单据表改造：`SettlementStatus` → `SettledAmount`（实体 + EF 配置同步，`numeric(18,2)` 默认 0）
- [ ] 1.4 增量迁移 `dotnet ef migrations add AddErpSettlement -p src/App.Infrastructure -s src/App.Api`，并在迁移内回填历史数据（`SettlementStatus = 1` → `SettledAmount = TotalAmount`）
- [ ] 1.5 删除 `OrderSettlementStatus` 枚举并清理全部引用

## 二、后端：仓储与用例

- [ ] 2.1 新增 `App.Core/Abstractions/ISettlementRepository.cs` + `App.Infrastructure/Repositories/SettlementRepository.cs`（`AddAsync` / `GetPagedAsync` / `GetDetailAsync` / `UpdateStatusAsync` / `GenerateSettlementNoAsync`）
- [ ] 2.2 新增 `App.Core/Abstractions/ISettlementQueryRepository.cs` + 读模型 `SettlementCandidateItem` / `ReconciliationItem` + 实现（未结候选 / 往来台账）
- [ ] 2.3 四类单据仓储：移除 `UpdateSettlementAsync`、新增 `AddSettledAmountAsync`（`ExecuteUpdateAsync` 原子累加）；列表 `GetPagedAsync` 的 `settlement` 参数改为按 `settlementState` 过滤
- [ ] 2.4 共享出参 `Features/Settlements/SettlementListItemDto.cs` / `SettlementDetailDto.cs` / `SettlementCandidateDto.cs` / `ReconciliationListItemDto.cs` + `SettlementsDtoMapper.cs`
- [ ] 2.5 新增用例 `Settlements/GetSettlements`
- [ ] 2.6 新增用例 `Settlements/CreateSettlement`（方向 / 往来 / 未结金额校验 → 落单 + 逐行累加 `SettledAmount`；同一事务）
- [ ] 2.7 新增用例 `Settlements/GetSettlementById`
- [ ] 2.8 新增用例 `Settlements/VoidSettlement`（逐行回退 `SettledAmount`）
- [ ] 2.9 新增用例 `Settlements/GetUnsettledOrders`（按方向映射可核销单据类型集合）
- [ ] 2.10 新增用例 `Settlements/GetReconciliation`（往来台账）
- [ ] 2.11 新增 `App.Api/Controllers/SettlementsController.cs`（6 端点；`unsettled-orders` 注册在 `{id:guid}` 之前）
- [ ] 2.12 移除四类单据的 `UpdateXxxSettlement` 用例、Controller 端点与 `DependencyInjection` 注册
- [ ] 2.13 四类单据 DTO 改造：`settlementStatus` → `settledAmount` + `unsettledAmount` + `settlementState`（`Mapper` 内推导）；列表请求参数与筛选同步
- [ ] 2.14 `App.Core` / `App.Infrastructure` `DependencyInjection.cs` 注册新用例、Validator 与两个新仓储

## 三、单元测试

- [ ] 3.1 `CreateSettlement` 成功：四种核销组合（收款 × 销售单 / 采购退货，付款 × 采购单 / 销售退货）各一例，断言单号前缀、总额重算、逐行累加、明细快照、`Commit`
- [ ] 3.2 `CreateSettlement` 异常：`40110` / `40112`（含单号与未结金额）/ `40113` / `40114` / `40400` / `40104` / `40108` / `40109`；失败路径无任何金额累加 + `RollbackAsync` 断言
- [ ] 3.3 `VoidSettlement`（回退累加 + 已作废 `40104` / 不存在 `40400`）、`GetSettlements` / `GetSettlementById`（筛选 / 分页 / 快照）
- [ ] 3.4 `GetUnsettledOrders` 方向 → 单据类型映射；`GetReconciliation` 应收 / 应付聚合（含退货冲减与已收 / 已付抵扣）与未结单据数
- [ ] 3.5 单据侧回归：四类单据 `SettlementState` 推导四态（0 / 部分 / 相等 / 超额）与 `settlementState` 筛选传参；**删除**四个 `UpdateXxxSettlement` 测试
- [ ] 3.6 Validator 边界 + `FieldValidationConsistencyTests` 扩展（`SettlementNo` 列长、`amount` 边界与精度、items 上限、方向枚举）
- [ ] 3.7 `cd backend && dotnet build` / `dotnet test` 通过（既有用例全部回归）

## 四、前端

- [ ] 4.1 `src/api/settlement.ts`（类型 + `getSettlements` / `createSettlement` / `getSettlementById` / `voidSettlement` / `getUnsettledOrders` / `getReconciliation`）
- [ ] 4.2 `src/views/SettlementManagement/SettlementsView.vue`（筛选 + 表格 + 作废）
- [ ] 4.3 `SettlementFormPage.vue`（类型 / 往来 / 日期 / 方式 + 可核销单据子表格 + 未结金额默认填充 + 「全部结清」+ `submitting`）
- [ ] 4.4 `SettlementDetailView.vue`（表头 + 核销明细只读 + 作废 + 404 空态）
- [ ] 4.5 `ReconciliationView.vue`（往来余额表格 + 未结单据抽屉）
- [ ] 4.6 `src/router/index.ts` 新增 `settlements` / `settlements/new` / `settlements/detail/:id` / `reconciliation`
- [ ] 4.7 `src/components/AppLayout.vue`「进销存」分组追加「收付款」「往来对账」+ `MENU_ROUTE_MAP` 增加 `settlementDetail`
- [ ] 4.8 单据四域前端改造：移除结算切换按钮与 `settlingId`，改为结算状态标签 + 未结金额，操作列新增「收付款」跳转（预置方向与往来单位），筛选「结算状态」下拉改为 未结算 / 部分结算 / 已结算
- [ ] 4.9 `src/api/purchase.ts` / `sale.ts` / `purchaseReturn.ts` / `saleReturn.ts`：移除 `updateXxxSettlement`，类型同步改造
- [ ] 4.10 结算状态标签文案与颜色映射抽到公共常量（`src/utils/` 或 `src/constants/`），供单据页与收付款页共用
- [ ] 4.11 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 五、E2E（Playwright）

- [ ] 5.1 新增 `e2e/settlement.spec.ts`：部分收款 → 销售单显示「部分结算（未结 x）」；收满 → 「已结算」；收付款单详情核销明细正确
- [ ] 5.2 同文件：作废收付款单 → 单据金额与状态回退；方向不匹配 / 超未结金额被拒（错误提示可见）
- [ ] 5.3 同文件：往来对账页应收 / 应付余额随收款变化，未结单据抽屉内容正确
- [ ] 5.4 改造既有 spec：`purchase.spec.ts` / `sale.spec.ts` / `purchase-return.spec.ts` / `sale-return.spec.ts` 中结算相关断言（移除手工切换用例，改为金额 / 状态标签 + 「去收付款」跳转断言）
- [ ] 5.5 `cd frontend && npm run test:e2e` 全量通过

## 六、规格与上下文联动

- [ ] 6.1 `specs/015-erp-purchase` / `016-erp-sale` / `021-erp-purchase-return` / `022-erp-sale-return` 的 `design.md` 加「演进（erp-settlement）」注记：结算由状态位升级为已结算金额、手工切换端点与按钮移除、列表筛选参数变更
- [ ] 6.2 `specs/003-api-swagger/design.md` 端点清单（若有相关断言）与 `tests/App.Tests` 中的端点断言同步
- [ ] 6.3 `.codebuddy/CONTEXT.md` §2（新 Feature / 仓储 / 读模型 / 错误码）、§3（新前端域目录与 api 文件）、§6（规格清单分类）同步
- [ ] 6.4 `specs/ROADMAP.md` 状态列更新（`023` → 已实现）

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
