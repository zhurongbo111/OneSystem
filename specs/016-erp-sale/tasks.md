---
created: 2026-09-13
updated: 2026-09-17
---

# 任务清单：销售出库（erp-sale）

> 依据 `specs/016-erp-sale/design.md` 拆分；按顺序实现，完成后勾选。
> 前置：**erp-purchase 已交付**（单据域共用模板：`OrderStatus` / `OrderSettlementStatus` / `OrderFieldConstraints` / `GenerateOrderNoAsync` / 共用错误码）；erp-product（`Inventory` + `TryDecrementAsync` / `GET /api/products/pick`）与 erp-partner 已交付。
> 后端：`cd backend`；前端：`cd frontend`。分支：`feature/erp-inventory`（当前分支，用户确认不另切分支）。
> 注意：照抄 erp-purchase 模板实现，**不重复定义**共用枚举 / 常量 / 单号生成（见 design §1 替换规则）。

## 一、后端

### 1.1 实体与数据模型

- [x] 1.1.1 新增实体 `App.Core/Entities/SalesOrder.cs`（同构 PurchaseOrder：OrderNo 唯一、PartnerId、PartnerName 客户快照、OrderDate、TotalAmount、SettlementStatus、Status、Remark、审计字段）
- [x] 1.1.2 新增实体 `App.Core/Entities/SalesOrderItem.cs`（同构 PurchaseOrderItem：OrderId 索引、ProductId、ProductName / Unit / UnitPrice 快照、Quantity、Subtotal）
- [x] 1.1.3 复用 `OrderStatus` / `OrderSettlementStatus`（**不新建**，确认 erp-purchase 已定义）
- [x] 1.1.4 复用 `OrderFieldConstraints` + `ProductFieldConstraints`（**不新建常量**）
- [x] 1.1.5 新增 EF 实体配置 2 个（`Persistence/Configurations/SalesOrderConfiguration.cs` / `SalesOrderItemConfiguration.cs`，照抄 Purchase 配置改实体类型）
- [x] 1.1.6 `AppDbContext` 注册 2 个 `DbSet`
- [x] 1.1.7 生成迁移 `AddErpSale` 并在 dev 库 `dotnet ef database update` 验证（表 / 索引 / 唯一约束 / 外键）

### 1.2 仓储接口与实现

- [x] 1.2.1 `ISalesOrderRepository` 接口 + `SalesOrderRepository`（同构 `IPurchaseOrderRepository`：GetPaged / GetDetail / Add / UpdateSettlement / UpdateStatus / GenerateOrderNo（前缀 `SO`））
- [x] 1.2.2 审计字段由 Handler 经 `ICurrentUser` 获取后随实体 / `operatorId` 参数传入仓储；跨仓储写在 Handler 中用 `IUnitOfWork` 事务

### 1.3 销售单 API

- [x] 1.3.1 `Sales` 用例 5 个：GetSalesOrders / CreateSalesOrder（40110、40400、40108、40109、40107；**逐行 TryDecrementAsync，失败 40103 回滚（message 含首个不足商品名）**；单号生成 `SO`；后端重算；事务：先扣库存成功再插单 + 明细）/ GetSalesOrderById / VoidSalesOrder（40104；回冲 +）/ UpdateSalesOrderSettlement（40104）+ Validator
- [x] 1.3.2 `SalesOrdersController`（5 路由）+ DI 注册（仓储 + Handler + Validator）

### 1.4 错误码与 Swagger

- [x] 1.4.1 `ErrorCode.cs` 追加 40103 `InsufficientStock`（常量 + 注释，见 design §3.2；40104 / 40107 / 40108 / 40109 / 40110 确认已存在）
- [x] 1.4.2 Swagger **不分组**（用户已确认）：维持现有单文档 Swagger，不使用 `ApiExplorerSettings.Group`

### 1.5 单元测试

- [x] 1.5.1 销售单用例测试（成功路径断言 TryDecrement 先于插单 + 单号 SO + 重算；**库存不足 40103 + Rollback + AddAsync 未被调用**；客户 / 商品校验；Void 回冲 + 40104；Settlement 40104）
- [x] 1.5.2 扩展 `FieldValidationConsistencyTests`（销售与采购 Validator 同字段边界一致；SalesOrders.OrderNo EF 长度 == 常量）
- [x] 1.5.3 `dotnet test` 全绿；`dotnet build` 0 警告

## 二、前端

### 2.1 接口层

- [x] 2.1.1 `src/api/sale.ts`（销售单类型 / 请求；开单提交不传小计 / 总额；日期范围转本地边界 UTC ISO；同 erp-purchase 约定）

### 2.2 销售页面（照抄 PurchaseManagement 模板，差异见 design §4）

- [x] 2.2.1 `SaleFormPage.vue`（客户下拉仅客户 / 两者；明细可编辑表格：商品下拉带库存展示、数量、单价默认带出销售价、小计 computed；**数量 > 库存行内标红预警**；添加 / 删除行；总额 computed；提交 loading + 成功跳详情）
- [x] 2.2.2 `SaleDetailView.vue`（descriptions + 明细只读表 + 作废 popconfirm（voidingId）+ 结算切换（未结算 / 已结算文案）+ 404 result）
- [x] 2.2.3 `SalesView.vue`（筛选含日期范围 / 结算 / 客户 + 表格（作废行置灰）+ 操作列（详情 / 作废 / 结算）+ 分页）

### 2.3 路由与菜单

- [x] 2.3.1 `router/index.ts` 新增 3 条路由（`sales` / `sales/new` / `sales/detail/:id`，见 design §4.3）
- [x] 2.3.2 `AppLayout.vue` 「进销存」分组追加子项「销售开单」+ `MENU_ROUTE_MAP` 增加 `salesDetail: 'sales'`

### 2.4 前端质量

- [x] 2.4.1 `npm run lint` 0 error；`npm run build` 成功
- [x] 2.4.2 手动联调走查：开销售单（客户 + 2 行明细）→ 数量超库存行内标红 → 提交被 40103 拦截（库存不变）→ 数量 ≤ 库存提交成功跳详情（单号 / 总额正确）→ 库存减少 → 标记已结算 → 作废回冲，全链路无 console 报错

## 三、E2E（Playwright）

### 3.1 销售出库

- [x] 3.1.1 `e2e/sale.spec.ts`：
  - 建商品 + 采购入库 → 库存 N
  - 开销售单（数量 ≤ N）→ 保存成功跳详情（单号 / 总额正确）→ 库存减少
  - **超卖场景**：数量 > 库存提交 → 错误提示「库存不足」→ 库存不变（未落库）
  - 标记已结算 → 作废确认 → 库存回冲 → 单据状态「已作废」且操作消失
- [x] 3.1.2 `npm run test:e2e` 全绿（含既有用例回归）

## 四、交付

- [x] 4.1 规格三件套最终一致性复查（代码与 design 表结构 / 错误码 / 路由逐条对照；与 erp-purchase 模板一致性核对）
> 提交 / 合并不列入待办：由用户主动发起指示（用户约定 2026-09-13）。
