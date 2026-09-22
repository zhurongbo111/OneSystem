---
created: 2026-09-16
updated: 2026-09-22
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
- [x] 2.2 `ISettlementRepository.GetPagedAsync` / 实现增 `orderType` + `orderId`（按核销明细反查，含已作废）
- [x] 2.3 新增 `App.Core/Abstractions/ISettlementQueryRepository.cs` + 读模型 `SettlementCandidateItem` / `ReconciliationItem` + 实现（未结候选 / 往来台账）
- [x] 2.4 `SettlementQueryRepository.GetReconciliationAsync` 改为按四表未结金额（`TotalAmount − SettledAmount`，仅未作废）归集应收 / 应付，删除 `Settlements` 聚合查询；`ReconciliationItem` 注释同步
- [x] 2.5 四类单据仓储：移除 `UpdateSettlementAsync`、新增 `AddSettledAmountAsync`（`ExecuteUpdateAsync` 原子累加）；列表 `GetPagedAsync` 的 `settlement` 参数改为按 `settlementState` 过滤
- [x] 2.6 四类单据仓储 `GetDetailAsync` 增 `bool includeItems = true`（置于 `CancellationToken` 之前）与 XML 文档注释（`false` 时 `Items` 恒为空集合、调用方不得消费）；EF 实现 `false` 时跳过明细查询（`AsNoTracking` 一次 `FirstOrDefaultAsync`）
- [x] 2.7 四类单据 16 处既有调用点补命名参数 `cancellationToken: cancellationToken`（`CreateXxx` / `GetXxxById` / `VoidXxx`，行为不变，编译期强制发现漏改）
- [x] 2.8 共享出参 `Features/Settlements/SettlementListItemDto.cs`（含 `OrderAmount` / `OrderTypes`）/ `SettlementDetailDto.cs` / `SettlementCandidateDto.cs` / `ReconciliationListItemDto.cs` + `SettlementsDtoMapper.cs`
- [x] 2.9 新增用例 `Settlements/GetSettlements`：一次批量取本页核销明细（非逐单 N+1），聚合「单据类型」列所需的 `OrderTypes`（去重升序），并兼作按单据反查的 `OrderAmount`
- [x] 2.10 新增用例 `Settlements/CreateSettlement`：往来存在 / 未结金额 / 方向与单据类型匹配校验 → 落单 + 逐行累加 `SettledAmount`（同一事务）；核销取数传 `includeItems: false`；**不校验往来档案类型与方向**（原 `40109` 移除）
- [x] 2.11 新增用例 `Settlements/GetSettlementById`
- [x] 2.12 新增用例 `Settlements/VoidSettlement`（逐行回退 `SettledAmount`）
- [x] 2.13 新增用例 `Settlements/GetUnsettledOrders`（按方向映射可核销单据类型集合）
- [x] 2.14 新增用例 `Settlements/GetReconciliation`（往来台账）
- [x] 2.15 `GetSettlementsRequest` 增可选 `OrderType` + `OrderId`；Validator 校验取值合法且**成对出现**（非法 `40000`）
- [x] 2.16 `App.Core/Errors/ErrorCode.cs` 追加 `40120 OrderSettledCannotVoid`
- [x] 2.17 四类单据作废用例（`VoidPurchaseReceipt` / `VoidSalesShipment` / `VoidPurchaseReturn` / `VoidSalesReturn`）在「已作废 `40104`」校验后追加「已核销 `40120`」校验（`SettledAmount > 0` 直接拒绝，不开事务）
- [x] 2.18 新增 `App.Api/Controllers/SettlementsController.cs`（6 端点；`unsettled-orders` 注册在 `{id:guid}` 之前）
- [x] 2.19 移除四类单据的 `UpdateXxxSettlement` 用例、Controller 端点与 `DependencyInjection` 注册
- [x] 2.20 四类单据 DTO 改造：`settlementStatus` → `settledAmount` + `unsettledAmount` + `settlementState`（`Mapper` 内推导）；列表请求参数与筛选同步
- [x] 2.21 `App.Core` / `App.Infrastructure` `DependencyInjection.cs` 注册新用例、Validator 与两个新仓储

## 三、单元测试

- [x] 3.1 `CreateSettlement` 成功：四种核销组合（收款 × 销售单 / 采购退货〔往来为纯 `Supplier`〕，付款 × 采购单 / 销售退货）各一例，断言单号前缀、总额重算、逐行累加、明细快照、`Commit`；不再因档案类型返回 `40109`
- [x] 3.2 `CreateSettlement` 取数范围（`includeItems`）：核销校验只取主表（主表查 1 次 / 明细查 0 次），四种核销方向与多行核销均断言；`*TestDoubles` 的 `GetDetailAsync` 支持 `includeItems`（`DetailQueries` 记录入参，`RecordingPurchaseReceiptRepository` 签名同步）；默认 `true` 时明细正常返回（既有详情用例回归）
- [x] 3.3 `CreateSettlement` 异常：`40110` / `40112`（含单号与未结金额）/ `40113` / `40114` / `40400` / `40104` / `40108`；失败路径无任何金额累加 + `RollbackAsync` 断言
- [x] 3.4 `VoidSettlement`（回退累加 + 已作废 `40104` / 不存在 `40400`）、`GetSettlements` / `GetSettlementById`（筛选 / 分页 / 快照；Handler 透传、`orderAmount` 计算、未传 `orderId` 不查询）、`SettlementRepositoryTests` 按单过滤（命中返回、未命中排除）
- [x] 3.5 `GetSettlements` 列表映射：`OrderTypes` 去重升序聚合（混合核销两类单据）、未按单据反查时 `OrderAmount` 为 null、空页不查明细且明细只批量查一次（非逐单 N+1）
- [x] 3.6 `GetUnsettledOrders` 方向 → 单据类型映射；`GetReconciliation` 按四表未结金额归集应收 / 应付（含退货冲减）与未结单据数，`SettlementQueryRepositoryTests` 同步重写对账断言
- [x] 3.7 「收款挂供应商」用例：纯 `Supplier` 往来（采购入库未结 + 采购退货已收）→ 应收 0 / 应付为未结合计（旧口径会算成负应收）；`收款核销采购退货` 成功例改用纯 `Supplier` 往来，删除「纯供应商 → `40109`」用例
- [x] 3.8 单据侧回归：四类单据 `SettlementState` 推导四态（0 / 部分 / 相等 / 超额）与 `settlementState` 筛选传参；**删除**四个 `UpdateXxxSettlement` 测试
- [x] 3.9 已核销禁作废：四类单据各补「已核销 → `40120` 且未开事务 / 未回冲 / 不写流水 / 状态不变」用例
- [x] 3.10 Validator 边界 + `FieldValidationConsistencyTests` 扩展（`SettlementNo` 列长、`amount` 边界与精度、items 上限、方向枚举）
- [x] 3.11 `cd backend && dotnet build` / `dotnet test` 通过（既有用例全部回归）

## 四、前端

- [x] 4.1 `src/api/settlement.ts`（类型 + `getSettlements` / `createSettlement` / `getSettlementById` / `voidSettlement` / `getUnsettledOrders` / `getReconciliation`）：`SettlementQuery` 含可选 `orderType` / `orderId`（成对），`SettlementListItem` 含 `orderAmount` 与 `orderTypes`
- [x] 4.2 `src/views/SettlementManagement/SettlementsView.vue`（筛选 + 表格 + 作废）；「单据类型」可选列**默认显示**（置于「类型」之后，多值以「、」连接），「创建时间」移出默认显示（保留列设置可选，与业务日期「收付日期」区分）
- [x] 4.3 `SettlementFormPage.vue`（类型 / 往来 / 日期 / 方式 + 可核销单据子表格 + 未结金额默认填充 + 「全部结清」+ `submitting`）；往来下拉取**全部启用往来**、不按类型过滤，切类型不清空往来
- [x] 4.4 `SettlementDetailView.vue`（表头 + 核销明细只读 + 作废 + 404 空态）
- [x] 4.5 `ReconciliationView.vue`（往来余额表格 + 未结单据抽屉）
- [x] 4.6 新增跨域共享组件 `components/SettlementRecords.vue`（只读 + 跳详情 + 作废行置灰 + `loading`）
- [x] 4.7 `src/router/index.ts` 新增 `settlements` / `settlements/new` / `settlements/detail/:id` / `reconciliation`
- [x] 4.8 `src/components/AppLayout.vue`「进销存」分组追加「收付款」「往来对账」+ `MENU_ROUTE_MAP` 增加 `settlementDetail`
- [x] 4.9 `src/utils/settlement.ts`（唯一来源）：结算状态标签文案与颜色、`settlementOrderTypeLabel` / `settlementOrderTypeRouteName`、`canStartSettlement`（正常且未结算）、`canVoidOrder`（正常且未核销）与 `VOID_SETTLED_HINT`
- [x] 4.10 单据四域前端改造：移除结算切换按钮与 `settlingId`，改为结算状态标签 + 未结金额；操作列「收付款」跳转（预置方向与往来单位，按 `canStartSettlement` 显隐）；「作废」按钮按 `canVoidOrder` 提前 `disabled` + tooltip；筛选「结算状态」下拉改为 未结算 / 部分结算 / 已结算
- [x] 4.11 `src/api/purchase.ts` / `sale.ts` / `purchaseReturn.ts` / `saleReturn.ts`：移除 `updateXxxSettlement`，类型同步改造
- [x] 4.12 四类单据详情接入「收付款明细」只读区块；`SettlementRecords.vue`、`SettlementDetailView.vue`（核销明细）、`ReconciliationView.vue`（未结单据抽屉）与四类单据详情的**单号改 `a-link` 超链接**并去掉「操作」列，「关联订单」同样改超链接
- [x] 4.13 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 五、E2E（Playwright）

- [x] 5.1 `e2e/settlement.spec.ts`：部分收款 → 销售单显示「部分结算（未结 x）」；收满 → 「已结算」；收付款单详情核销明细正确
- [x] 5.2 同文件：作废收付款单 → 单据金额与状态回退；方向不匹配 / 超未结金额被拒（错误提示可见）
- [x] 5.3 同文件：往来对账页应收 / 应付余额随收款变化，未结单据抽屉内容正确
- [x] 5.4 同文件：销售出库详情「收付款明细」可见该笔收付款单并可跳详情
- [x] 5.5 同文件：供应商采购退货 → 收款（可选中该供应商，不再 `40109`）与对账余额断言
- [x] 5.6 同文件：销售单收款后作废被拒（提示可见）→ 作废收款单回退 → 再作废成功
- [x] 5.7 同文件：收付款列表该行含「销售出库单」（单据类型列）；默认表头含「单据类型」、不含「创建时间」
- [x] 5.8 同文件：已结算后「收付款」入口消失、作废收款单回退后恢复；已核销时「作废」按钮 `disabled`（已结算退货单详情无「去收付款」）
- [x] 5.9 `settlement.spec.ts` / `purchase-order.spec.ts` / `sale-order.spec.ts`：单号链接存在 / 点击跳转，关联订单超链接回跳
- [x] 5.10 改造既有 spec：`purchase.spec.ts` / `sale.spec.ts` / `purchase-return.spec.ts` / `sale-return.spec.ts` 中结算相关断言（移除手工切换用例，改为金额 / 状态标签 + 「去收付款」跳转断言）
- [x] 5.11 `cd frontend && npm run test:e2e` 全量通过

## 六、规格与上下文联动

- [x] 6.1 `specs/015-erp-purchase` / `016-erp-sale` / `021-erp-purchase-return` / `022-erp-sale-return` 的 `design.md` 加「演进（erp-settlement）」注记：结算由状态位升级为已结算金额、手工切换端点与按钮移除、列表筛选参数变更、核销取数只查主表（`includeItems`）
- [x] 6.2 `specs/015` / `016` / `021` / `022` / `024` 的 `design.md` 加「演进（erp-settlement，已核销禁作废）」注记
- [x] 6.3 `specs/003-api-swagger/design.md` 端点清单（若有相关断言）与 `tests/App.Tests` 中的端点断言同步
- [x] 6.4 `.codebuddy/CONTEXT.md` 同步：§2（新 Feature / 仓储 / 读模型 / 错误码 `40120`；结算口径——收款不受往来类型限制、对账按四表未结金额归集）、§3（新前端域目录与 api 文件）、§6（规格清单分类）
- [x] 6.5 `specs/ROADMAP.md` 状态列更新（`023` → 已实现）

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
