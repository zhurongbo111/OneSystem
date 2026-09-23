---
created: 2026-09-13
updated: 2026-09-23
---

# 任务清单：商品管理（erp-product）

> 依据 `specs/012-erp-product/design.md` 拆分；按顺序实现，完成后勾选。
> 后端：`cd backend`；前端：`cd frontend`。分支：`feature/erp-inventory`（用户确认在当前分支开发）。
> 提交 / 合并不列入待办：由用户主动发起指示（2026-09-13 用户约定）。

## 一、后端

### 1.1 实体与数据模型

- [x] 1.1.1 新增枚举 `App.Core/Entities/ProductStatus.cs`
- [x] 1.1.2 新增实体 `App.Core/Entities/Category.cs`（Id / Name / CreatedAt）
- [x] 1.1.3 新增实体 `App.Core/Entities/Product.cs`（含 Code 唯一、CategoryId、Unit、PurchasePrice、SalePrice、SafetyStock、Status、审计字段）
- [x] 1.1.4 新增实体 `App.Core/Entities/Inventory.cs`（ProductId 唯一、Quantity 可负、UpdatedAt）
- [x] 1.1.5 新增字段约束常量：`CategoryFieldConstraints`、`ProductFieldConstraints`（值见 design §2.5）
- [x] 1.1.6 新增 EF 实体配置 3 个（`Persistence/Configurations/*Configuration.cs`）：列类型 / 长度 / 索引（Code / Name / ProductId 唯一）
- [x] 1.1.7 `AppDbContext` 注册 3 个 `DbSet`
- [x] 1.1.8 生成迁移 `AddErpProduct` 并在 dev 库 `dotnet ef database update` 验证（表 / 索引 / 唯一约束 / 外键）

### 1.2 仓储接口与实现

- [x] 1.2.1 `ICategoryRepository` 接口 + `CategoryRepository`（GetAll / GetById / ExistsByName（忽略大小写）/ Add / Update / Delete / ReferencedByProducts）
- [x] 1.2.2 `IProductRepository` 接口 + `ProductRepository`（GetById / ExistsByCode / GetPaged（联查 Inventory 出 stockQuantity）/ GetDetail / Add / Update / GetPickList（仅启用，联查当前库存））
- [x] 1.2.3 `IInventoryRepository` 接口 + `InventoryRepository`（Add / **IncrementAsync（ExecuteUpdate 原子加）** / **TryDecrementAsync（ExecuteUpdate 条件减，返回受影响行）**）
- [x] 1.2.4 审计字段由 Handler 经 `ICurrentUser` 获取后随实体传入仓储；跨仓储写在 Handler 中用 `IUnitOfWork` 事务

### 1.3 商品与分类 API

- [x] 1.3.1 `Categories` 用例 4 个：GetCategories / CreateCategory（重名 40105）/ UpdateCategory / DeleteCategory（有引用 40106、不存在 40400）+ Validator
- [x] 1.3.2 `Products` 用例 6 个：GetProducts（筛选 + 低库存映射）/ CreateProduct（40101、分类 40400、同事务建库存 0 行）/ GetProductById / UpdateProduct（不可改 Code）/ UpdateProductStatus / GetProductPickList + Validator
- [x] 1.3.3 `ProductsController`（6 路由）+ `CategoriesController`（4 路由），`[Authorize]`、`[ApiExplorerSettings]`
- [x] 1.3.4 DI 注册（仓储 + Handler + Validator）

### 1.4 错误码与 Swagger

- [x] 1.4.1 `ErrorCode.cs` 追加 40101 / 40105 / 40106 / 40107（常量 + 注释，见 design §3.2）
- [x] 1.4.2 Swagger 分组 — 用户已确认**不分组**：现有 `UsersController` 等均为单文档 Swagger、无 `Group` 属性，本规格维持单文档现状（design §3.6 已同步更新）。

### 1.5 单元测试

- [x] 1.5.1 分类用例测试（Create 重名 / Delete 有引用 / Update / Get）
- [x] 1.5.2 商品用例测试（Create 40101 + 建库存行 / 列表低库存映射三态（阈值 0、库存 = 阈值、库存 < 阈值）/ Update 不改 Code / Pick 仅启用）
- [x] 1.5.3 扩展 `FieldValidationConsistencyTests`（EF 长度 == 常量；Validator 边界通过 / 拒绝；keyword ≤ 50）
- [x] 1.5.4 `dotnet test` 全绿；`dotnet build` 0 警告

## 二、前端

### 2.1 接口层

- [x] 2.1.1 `src/api/product.ts`（商品 + 分类类型 / 请求；含 pick 列表、状态切换、分类 CRUD）

### 2.2 商品管理

- [x] 2.2.1 `ProductFormDrawer.vue`（打开重置；分类下拉 + 就地行内新建分类；金额 input-number 约束；编辑态 Code 只读）
- [x] 2.2.2 `ProductsView.vue`（筛选行 + 表格含库存列低库存标红 + 状态列 + 操作列（编辑 / 启停 popconfirm / 详情（复用 `ProductFormDrawer` 查看态））+ 服务端分页 + 列设置）
- [x] 2.2.3 分类维护迁出为独立页面（`CategoryManagement/`，见 `specs/017-erp-category/`）：移除商品页工具条「分类管理」按钮、删除 `CategoryManagerModal.vue` 与对应 e2e 用例，入口统一走侧边菜单

### 2.3 路由与菜单

- [x] 2.3.1 `router/index.ts` 新增 `products` 路由
- [x] 2.3.2 `AppLayout.vue` 新增「进销存」子菜单（key `erp`，图标 `IconStorage`，默认展开）+ 子项「商品管理」；分类管理子项见 `specs/017-erp-category/`

### 2.4 前端质量

- [x] 2.4.1 `npm run lint` 0 error；`npm run build` 成功
- [x] 2.4.2 联调走查：建分类 → 建商品 → 列表库存 0 → 重名编码提示 → 编辑 / 启停 / 分类删除拦截 — **由 `e2e/product.spec.ts` 自动化覆盖**（含重名编码 40101、启停、分类增删改拦截）

## 三、E2E（Playwright）

### 3.1 商品管理

- [x] 3.1.1 `e2e/product.spec.ts`：登录 → 建分类 → 建商品（编码 / 价格 / 安全库存）→ 列表展示库存 0 → 重名编码 40101 提示 → 编辑 / 详情 / 启停 / 空态与重置 / 分类增删改 / 查询 loading
- [x] 3.1.2 `npm run test:e2e` 全绿（含既有用例回归）

## 四、交付

- [x] 4.1 规格三件套最终一致性复查（代码与 design 表结构 / 错误码 / 路由逐条对照；Swagger 分组与分支按用户确认修订）
- [x] 4.2 前端 lint / build / e2e 与后端 `dotnet test` 全绿，临时文件与 dev 服务已清理
