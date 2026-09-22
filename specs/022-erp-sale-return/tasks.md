---
created: 2026-09-16
updated: 2026-09-22
---

# 任务清单：销售退货（erp-sale-return）

> 依据 `specs/022-erp-sale-return/design.md` 拆分（结构继承 `021-erp-purchase-return`）。含后端新单据域（库存回增 + 流水）+ 5 个用例 + 前端列表 / 开单 / 详情。按顺序实现，完成后勾选。
> 前置：`019-erp-stock-movement` 已实现（消费流水仓储）；建议在 `021-erp-purchase-return` 之后实现（照抄其模板，替换客户 / 销售价 / 库存方向）。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与仓储

- [x] 1.1 新增 `App.Core/Entities/SalesReturn.cs`、`SalesReturnItem.cs`（枚举复用）
- [x] 1.2 `Persistence/Configurations/SalesReturnConfiguration.cs`、`SalesReturnItemConfiguration.cs` + `AppDbContext` 新增 2 个 `DbSet`
- [x] 1.3 新增 `App.Core/Abstractions/ISalesReturnRepository.cs` + `App.Infrastructure/Repositories/SalesReturnRepository.cs`（照抄 `PurchaseReturnRepository`，前缀 `SR`）+ `DependencyInjection.cs` 注册
- [x] 1.4 增量迁移 `dotnet ef migrations add AddErpSaleReturn -p src/App.Infrastructure -s src/App.Api`

## 二、后端：流水类型与用例

- [x] 2.1 `StockMovementType` 追加 `SalesReturnIn = 9`、`SalesReturnVoid = 10`
- [x] 2.2 共享出参 `Features/SalesReturns/SalesReturnListItemDto.cs` / `SalesReturnDetailDto.cs` + `SalesReturnsDtoMapper.cs`
- [x] 2.3 新增用例 `SalesReturns/GetSalesReturns`
- [x] 2.4 新增用例 `SalesReturns/CreateSalesReturn`（客户 / 商品校验 → 生成单号 → 落单 + 明细 → 逐行回增库存 + 流水；同一事务）
- [x] 2.5 新增用例 `SalesReturns/GetSalesReturnById`
- [x] 2.6 新增用例 `SalesReturns/VoidSalesReturn`（回冲 `IncrementAsync(-q)`，允许冲负 + 反向流水）
- [x] 2.7 新增用例 `SalesReturns/UpdateSalesReturnSettlement`
- [x] 2.8 新增 `App.Api/Controllers/SalesReturnsController.cs`（5 端点）
- [x] 2.9 `App.Core/DependencyInjection.cs` 注册 5 个 Handler 与对应 Validator

## 三、单元测试

- [x] 3.1 `CreateSalesReturn` 成功：单号 `SR`、快照、金额重算、逐行回增 + 流水（`SalesReturnIn` 正方向 + 来源）、`Commit`
- [x] 3.2 `CreateSalesReturn` 异常：明细空 `40110` / 客户 `40400` `40108` `40109` / 商品 `40400` `40107`；失败路径**不写流水**、不写库存、`RollbackAsync` 断言
- [x] 3.3 `VoidSalesReturn`（回冲负方向 + `SalesReturnVoid` + 允许冲负场景 / 已作废 `40104` / 不存在 `40400`）、`UpdateSalesReturnSettlement`
- [x] 3.4 `GetSalesReturns`（客户维度筛选传参 / 分页映射）、`GetSalesReturnById`（明细快照 / `40400`）
- [x] 3.5 Validator 边界 + `FieldValidationConsistencyTests` 扩展（采购退货 / 销售退货同字段边界一致）
- [x] 3.6 对账一致性：销售出库 → 退货 → 退货作废后 `Σ 流水变动量 == Inventory.Quantity`
- [x] 3.7 `cd backend && dotnet build` / `dotnet test` 通过（既有用例全部回归，317/317）

## 四、前端

- [x] 4.1 `src/api/saleReturn.ts`（类型 + 5 个请求函数）
- [x] 4.2 `src/views/SalesReturnManagement/SalesReturnsView.vue`（筛选行客户 + 表格 + 作废 / 结算操作）
- [x] 4.3 `SalesReturnFormPage.vue`（客户 + 明细子表格 + 销售价带出 + 无库存预警 + `submitting`）
- [x] 4.4 `SalesReturnDetailView.vue`（表头 + 明细只读 + 作废 / 结算 + 404 空态）
- [x] 4.5 `src/router/index.ts` 新增 `sales-returns` / `sales-returns/new` / `sales-returns/detail/:id`
- [x] 4.6 `src/components/AppLayout.vue`「进销存」分组追加子项「销售退货」+ `MENU_ROUTE_MAP` 增加 `saleReturnDetail`
- [x] 4.7 `StockMovementsView.vue` 变动类型下拉与展示支持新增两种类型（依据 `specs/019-erp-stock-movement/design.md` §0 表）
- [x] 4.8 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 五、E2E（Playwright）

- [x] 5.1 新增 `e2e/sale-return.spec.ts`：开退货单 → 库存增加，流水出现 `+N`「销售退货」+ 来源单号
- [x] 5.2 同文件：作废该单 → 库存回冲，流水出现 `-N`「销售退货作废」；已作废行置灰；另覆盖「作废允许冲负」场景
- [x] 5.3 同文件：列表筛选（单号 / 客户 / 结算状态 / 日期范围）、详情展示、结算切换
- [x] 5.4 `cd frontend && npm run test:e2e` 全量通过（98/98，含采购 / 销售 / 退货 / 库存 / 流水既有用例回归；在清理后的干净 dev 库上验证）

## 六、规格与上下文联动

- [x] 6.1 `specs/019-erp-stock-movement/design.md` §0 表续行：`SalesReturnIn = 9`「销售退货」`cyan`、`SalesReturnVoid = 10`「销售退货作废」`pinkpurple`（随本规格起草同步）
- [x] 6.2 `.codebuddy/CONTEXT.md` §2 / §3 / §6 同步
- [x] 6.3 `specs/ROADMAP.md` 状态列更新（`022` → 已实现）

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
