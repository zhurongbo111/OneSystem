---
created: 2026-09-13
updated: 2026-09-17
---

# 任务清单：库存查询（erp-inventory-query）

> 依据 `specs/014-erp-inventory-query/design.md` 拆分；按顺序实现，完成后勾选。
> 前置：erp-product 已交付（`Inventory` 表 / `IInventoryRepository` 存在）。
> 后端：`cd backend`；前端：`cd frontend`。分支：`feature/erp-inventory`（当前分支，用户确认不另切分支）。

## 一、后端

### 1.1 仓储扩展

- [x] 1.1.1 `IInventoryRepository` 追加 `GetPagedAsync(string? keyword, Guid? categoryId, int page, int pageSize, ...)` 只读方法（联查 Inventory / Products / Categories，仅启用商品，`ORDER BY Products.Code ASC`）
- [x] 1.1.2 `InventoryRepository` 实现 `GetPagedAsync`（EF Core 投影联查，不写裸 SQL）

### 1.2 库存查询 API

- [x] 1.2.1 `Inventory/GetInventory` 用例（关键词 / 分类筛选、启用商品过滤透传、低库存映射 `safetyStock > 0 && stockQuantity < safetyStock`）+ Validator（keyword ≤ 50，引用 `ProductFieldConstraints.KeywordMaxLength`）
- [x] 1.2.2 `InventoryController`（1 路由）+ DI 注册

### 1.3 错误码与 Swagger

- [x] 1.3.1 无新增错误码（核对 `ErrorCode.cs` 不改动）
- [x] 1.3.2 Swagger **不分组**（用户已确认）：维持现有单文档 Swagger，不使用 `ApiExplorerSettings.Group`

### 1.4 单元测试

- [x] 1.4.1 库存查询用例测试（筛选传参 / 低库存映射三态 / 分页 total 透传）
- [x] 1.4.2 扩展 `FieldValidationConsistencyTests`（keyword 边界 50 通过 / 51 拒绝，与 `ProductFieldConstraints.KeywordMaxLength` 一致）
- [x] 1.4.3 `dotnet test` 全绿（165）；构建无错误（dev server 锁 DLL 期间以 `-p:OutputPath` 旁路构建验证）

## 二、前端

### 2.1 接口层

- [x] 2.1.1 `src/api/inventory.ts`（库存查询类型 / 请求）

### 2.2 库存查询页

- [x] 2.2.1 `InventoryView.vue`（只读列表 + 低库存标红 / 标签 + 关键词 / 分类筛选 + 分页）

### 2.3 路由与菜单

- [x] 2.3.1 `router/index.ts` 新增 `inventory` 路由
- [x] 2.3.2 `AppLayout.vue` 「进销存」分组追加子项「库存查询」

### 2.4 前端质量

- [x] 2.4.1 `npm run lint` 0 error；`npm run build` 成功
- [x] 2.4.2 手动联调走查：库存列表展示 → 关键词 / 分类筛选 → 低库存行标红 → 停用商品不出现（需先经 erp-product 停用一商品验证），全链路无 console 报错（已由 e2e 全量覆盖）

## 三、E2E（Playwright）

### 3.1 库存查询

- [x] 3.1.1 `e2e/inventory.spec.ts`：登录 → 建商品（含安全阈值）→ 库存页展示（库存 0、阈值 > 0 → 标红 + 标签）→ 关键词 / 分类筛选 → 停用商品后库存页不再出现
- [x] 3.1.2 `npm run test:e2e` 全绿（含既有用例回归，72 passed）

## 四、交付

- [x] 4.1 规格三件套最终一致性复查（代码与 design 接口 / DTO 字段 / 路由逐条对照）
> 提交 / 合并不列入待办：由用户主动发起指示（用户约定 2026-09-13）。
