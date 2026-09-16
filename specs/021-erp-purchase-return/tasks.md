---
created: 2026-09-16
updated: 2026-09-16
---

# 任务清单：采购退货（erp-purchase-return）

> 依据 `specs/021-erp-purchase-return/design.md` 拆分。含后端新单据域（含库存扣减 + 流水）+ 5 个用例 + 前端列表 / 开单 / 详情。按顺序实现，完成后勾选。
> 前置：`019-erp-stock-movement` 已实现（本规格消费其 `IStockMovementRepository`）。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与仓储

- [ ] 1.1 新增 `App.Core/Entities/PurchaseReturn.cs`、`PurchaseReturnItem.cs`（枚举复用 `OrderStatus` / `OrderSettlementStatus`）
- [ ] 1.2 `Persistence/Configurations/PurchaseReturnConfiguration.cs`、`PurchaseReturnItemConfiguration.cs` + `AppDbContext` 新增 2 个 `DbSet`
- [ ] 1.3 新增 `App.Core/Abstractions/IPurchaseReturnRepository.cs`（`GetPagedAsync` / `GetDetailAsync` / `AddAsync` / `UpdateSettlementAsync` / `UpdateStatusAsync` / `GenerateReturnNoAsync`）+ `App.Infrastructure/Repositories/PurchaseReturnRepository.cs` + `DependencyInjection.cs` 注册
- [ ] 1.4 增量迁移 `dotnet ef migrations add AddErpPurchaseReturn -p src/App.Infrastructure -s src/App.Api`

## 二、后端：流水类型与用例

- [ ] 2.1 `StockMovementType` 追加 `PurchaseReturnOut = 7`、`PurchaseReturnVoid = 8`
- [ ] 2.2 共享出参 `Features/PurchaseReturns/PurchaseReturnListItemDto.cs` / `PurchaseReturnDetailDto.cs` + `PurchaseReturnsDtoMapper.cs`
- [ ] 2.3 新增用例 `PurchaseReturns/GetPurchaseReturns`（Request + Validator + Handler + Response）
- [ ] 2.4 新增用例 `PurchaseReturns/CreatePurchaseReturn`（供应商 / 商品校验 → 先扣库存 `TryDecrementAsync` → 落单 + 明细 → 逐行写流水；同一事务）
- [ ] 2.5 新增用例 `PurchaseReturns/GetPurchaseReturnById`
- [ ] 2.6 新增用例 `PurchaseReturns/VoidPurchaseReturn`（回冲 `IncrementAsync(+q)` + 反向流水）
- [ ] 2.7 新增用例 `PurchaseReturns/UpdatePurchaseReturnSettlement`
- [ ] 2.8 新增 `App.Api/Controllers/PurchaseReturnsController.cs`（5 端点）
- [ ] 2.9 `App.Core/DependencyInjection.cs` 注册 5 个 Handler 与对应 Validator

## 三、单元测试

- [ ] 3.1 `CreatePurchaseReturn` 成功：扣减先于插入、单号 `PR`、快照、金额重算、逐行流水（负方向 + 来源）、`Commit`
- [ ] 3.2 `CreatePurchaseReturn` 异常：库存不足 `40103`（含商品名）/ 明细空 `40110` / 供应商 `40400` `40108` `40109` / 商品 `40400` `40107`；失败路径**不写流水**、`RollbackAsync` 断言
- [ ] 3.3 `VoidPurchaseReturn`（回冲 + `PurchaseReturnVoid` 正方向 + 已作废 `40104` / 不存在 `40400`）、`UpdatePurchaseReturnSettlement`（成功 / `40104` / `40400`）
- [ ] 3.4 `GetPurchaseReturns`（筛选传参 / 分页映射）、`GetPurchaseReturnById`（明细快照 / `40400`）
- [ ] 3.5 Validator 边界 + `FieldValidationConsistencyTests` 扩展（`ReturnNo` 列长、quantity / unitPrice / items / keyword 与采购销售同源）
- [ ] 3.6 对账一致性：采购入库 → 退货 → 退货作废后 `Σ 流水变动量 == Inventory.Quantity`
- [ ] 3.7 `cd backend && dotnet build` / `dotnet test` 通过（既有用例全部回归）

## 四、前端

- [ ] 4.1 `src/api/purchaseReturn.ts`（类型 + 5 个请求函数）
- [ ] 4.2 `src/views/PurchaseReturnManagement/PurchaseReturnsView.vue`（筛选行 + 操作行 + 表格 + 服务端分页 + 作废 / 结算操作）
- [ ] 4.3 `PurchaseReturnFormPage.vue`（独立开单页：供应商 + 明细子表格 + 金额展示 + 退货数量 > 库存行内预警 + `submitting`）
- [ ] 4.4 `PurchaseReturnDetailView.vue`（表头 + 明细只读 + 作废 / 结算 + 404 空态）
- [ ] 4.5 `src/router/index.ts` 新增 `purchase-returns` / `purchase-returns/new` / `purchase-returns/detail/:id`
- [ ] 4.6 `src/components/AppLayout.vue`「进销存」分组追加子项「采购退货」+ `MENU_ROUTE_MAP` 增加 `purchaseReturnDetail`
- [ ] 4.7 `StockMovementsView.vue` 变动类型下拉与展示支持新增两种类型（依据 `specs/019-erp-stock-movement/design.md` §0 表）
- [ ] 4.8 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 五、E2E（Playwright）

- [ ] 5.1 新增 `e2e/purchase-return.spec.ts`：开退货单（选供应商 + 商品 + 数量）→ 库存减少，流水出现 `-N`「采购退货」+ 来源单号
- [ ] 5.2 同文件：作废该单 → 库存回冲，流水出现 `+N`「采购退货作废」；已作废行置灰
- [ ] 5.3 同文件：退货数量 > 库存 → 行内预警可见 + 提交被拒并提示库存不足；列表筛选（单号 / 供应商 / 结算状态 / 日期范围）与详情展示、结算切换
- [ ] 5.4 `cd frontend && npm run test:e2e` 全量通过（含采购 / 销售 / 库存 / 流水既有用例回归）

## 六、规格与上下文联动

- [x] 6.1 `specs/019-erp-stock-movement/design.md` §0 表续行：`PurchaseReturnOut = 7`「采购退货」`orangered`、`PurchaseReturnVoid = 8`「采购退货作废」`magenta`（随本规格起草同步）
- [ ] 6.2 `.codebuddy/CONTEXT.md` §2（新 Feature / 仓储 / 错误码）、§3（新前端域目录与 api 文件）、§6（规格清单分类）同步
- [ ] 6.3 `specs/ROADMAP.md` 状态列更新（`021` → 已实现）

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
