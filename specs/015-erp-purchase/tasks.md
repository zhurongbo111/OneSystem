---
created: 2026-09-13
updated: 2026-09-16
---

# 任务清单：采购入库（erp-purchase）

> 依据 `specs/015-erp-purchase/design.md` 拆分；按顺序实现，完成后勾选。
> 前置：erp-product（`Products` / `Inventory` / `GET /api/products/pick`）与 erp-partner（`Partners` + 类型 / 状态筛选）已交付。
> 后端：`cd backend`；前端：`cd frontend`。分支：`feature/erp-inventory`（当前分支，用户确认不另切分支）。
> 注意：本规格建立**单据域共用模板**（实体 / 仓储 / 单号 / 校验 / 共用错误码），erp-sale 照抄本规格实现，勿在后续规格重复定义。

## 一、后端

### 1.1 实体与数据模型

- [x] 1.1.1 新增枚举 `App.Core/Entities/OrderStatus.cs`（`Voided = 0, Normal = 1`）、`OrderSettlementStatus.cs`（`Unsettled = 0, Settled = 1`）（采购 / 销售共用）
- [x] 1.1.2 新增实体 `App.Core/Entities/PurchaseOrder.cs`（OrderNo 唯一、PartnerId、PartnerName 快照、OrderDate、TotalAmount、SettlementStatus、Status、Remark、审计字段）
- [x] 1.1.3 新增实体 `App.Core/Entities/PurchaseOrderItem.cs`（OrderId 索引、ProductId、ProductName / Unit / UnitPrice 快照、Quantity、Subtotal）
- [x] 1.1.4 新增字段约束常量：`OrderFieldConstraints`（OrderNoMaxLength / RemarkMaxLength / KeywordMaxLength=20 / ItemsMaxCount=100，见 design §2.4；quantity / unitPrice 边界引用 `ProductFieldConstraints`，禁止复制）
- [x] 1.1.5 新增 EF 实体配置 2 个（`Persistence/Configurations/PurchaseOrderConfiguration.cs` / `PurchaseOrderItemConfiguration.cs`）：列类型 / 长度 / 索引（OrderNo 唯一；明细 OrderId 索引）
- [x] 1.1.6 `AppDbContext` 注册 2 个 `DbSet`
- [x] 1.1.7 生成迁移 `AddErpPurchase` 并在 dev 库 `dotnet ef database update` 验证（表 / 索引 / 唯一约束 / 外键）

### 1.2 仓储接口与实现

- [x] 1.2.1 `IPurchaseOrderRepository` 接口 + `PurchaseOrderRepository`（GetPaged / GetDetail（含明细）/ Add（单 + 明细）/ UpdateSettlement / UpdateStatus / GenerateOrderNo（见 design §3.6，前缀参数化））
- [x] 1.2.2 审计字段由 Handler 经 `ICurrentUser` 获取后随实体 / `operatorId` 参数传入仓储；跨仓储写在 Handler 中用 `IUnitOfWork` 事务

### 1.3 采购单 API

- [x] 1.3.1 `Purchases` 用例 5 个：GetPurchaseOrders / CreatePurchaseOrder（40110、40400、40108、40109、40107；单号生成；后端重算小计 / 总额；事务：插单 + 明细 + 库存 +=）/ GetPurchaseOrderById / VoidPurchaseOrder（40104；回冲 -）/ UpdatePurchaseOrderSettlement（40104）+ Validator
- [x] 1.3.2 `PurchaseOrdersController`（5 路由）+ DI 注册（仓储 + Handler + Validator）

### 1.4 错误码与 Swagger

- [x] 1.4.1 `ErrorCode.cs` 追加 40104 / 40108 / 40109 / 40110（常量 + 注释，见 design §3.2；40107 由 erp-product 已定义，确认存在）
- [x] 1.4.2 Swagger **不分组**（用户已确认）：维持现有单文档 Swagger，不使用 `ApiExplorerSettings.Group`

### 1.5 单元测试

- [x] 1.5.1 采购单用例测试（成功路径断言单号 + 重算 + 库存 += 调用顺序；40110 / 40400 / 40108 / 40109 / 40107；Void 40104 + 回冲调用；Settlement 40104；Commit 失败 Rollback）
- [x] 1.5.2 扩展 `FieldValidationConsistencyTests`（OrderNo EF 长度 == 常量；quantity / unitPrice / items 行边界通过 / 拒绝；keyword 20/21）
- [x] 1.5.3 `dotnet test` 全绿；`dotnet build` 0 警告

## 二、前端

### 2.1 接口层

- [x] 2.1.1 `src/api/purchase.ts`（采购单类型 / 请求；开单提交不传小计 / 总额；日期范围转本地边界 UTC ISO）

### 2.2 采购页面

- [x] 2.2.1 `PurchaseFormPage.vue`（供应商下拉仅供应商 / 两者；明细可编辑表格：商品下拉带库存展示、数量、单价默认带出采购价、小计 computed；添加 / 删除行；总额 computed；提交 loading + 成功跳详情）
- [x] 2.2.2 `PurchaseDetailView.vue`（descriptions + 明细只读表 + 作废 popconfirm（voidingId）+ 结算切换 + 404 result）
- [x] 2.2.3 `PurchasesView.vue`（筛选含日期范围 / 结算 / 供应商 + 表格（作废行置灰）+ 操作列（详情 / 作废 / 结算）+ 分页）

### 2.3 路由与菜单

- [x] 2.3.1 `router/index.ts` 新增 3 条路由（`purchases` / `purchases/new` / `purchases/detail/:id`，见 design §4.3）
- [x] 2.3.2 `AppLayout.vue` 「进销存」分组追加子项「采购入库」+ `MENU_ROUTE_MAP` 增加 `purchaseDetail: 'purchases'`

### 2.4 前端质量

- [x] 2.4.1 `npm run lint` 0 error；`npm run build` 成功
- [x] 2.4.2 手动联调走查：开采购单（供应商 + 2 行明细，改一行单价）→ 保存跳详情（单号 / 总额正确）→ 库存增加 → 标记已结算 → 作废回冲 → 作废后操作消失，全链路无 console 报错

## 三、E2E（Playwright）

### 3.1 采购入库

- [x] 3.1.1 `e2e/purchase.spec.ts`：
  - 开单（供应商 + 2 行明细，改一行单价）→ 保存成功跳详情（单号 / 总额正确）
  - 库存页数量增加 → 标记已结算
  - 超卖前置：建商品 → 采购入库 → 销售出库（依赖 erp-sale；若 erp-sale 未交付则本用例仅覆盖采购链路）
  - 作废采购单（确认）→ 库存回冲 → 单据状态「已作废」且操作消失
- [x] 3.1.2 `npm run test:e2e` 全绿（含既有用例回归）

## 四、交付

- [x] 4.1 规格三件套最终一致性复查（代码与 design 表结构 / 错误码 / 路由逐条对照）
> 提交 / 合并不列入待办：由用户主动发起指示（用户约定 2026-09-13）。
