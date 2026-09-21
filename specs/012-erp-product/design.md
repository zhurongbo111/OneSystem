---
created: 2026-09-13
updated: 2026-09-16
---

# 设计规格：商品管理（erp-product）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `user-management` 为结构参照；字段约束单一来源（后端规则 §5.3）同样适用。
> 本规格为进销存功能组商品域底座，库存台账与开单商品选择接口的消费方为 erp-inventory-query / erp-purchase / erp-sale。

## 1. 总体设计

```
商品 / 分类 / 库存台账（前端 /products）
  → ProductsController / CategoriesController
    → App.Core/Features/Products|Categories/<Action>/*RequestHandler
      → IProductRepository / ICategoryRepository / IInventoryRepository（App.Core）
        → 仓储实现（App.Infrastructure，EF Core）→ AppDbContext → PostgreSQL
```

核心原则：

- **库存独立成表** `Inventory`，与 `Products` **一对一**（`ProductId` 唯一）；商品新建时同步初始化一行库存（`Quantity = 0`）。
- **禁止负库存**由消费方（erp-sale 的销售扣减）通过条件扣减 `TryDecrementAsync` 实现；`IncrementAsync` 的 `delta` 允许为负（采购作废回冲场景，由 erp-purchase 消费）。
- 商品 / 分类不删除商品，商品只停用；分类可删除但被商品引用时拒绝。

## 2. 数据模型

> 时间字段按后端规则 §5.2（`DateTimeOffset` → `timestamptz`）。
> 状态枚举统一 `Enabled = 1 / Disabled = 0` 小整数，PG `smallint`。

### 2.1 实体 `App.Core/Entities/Category.cs` 与表 `Categories`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `Name` | `string` | `varchar(20)` | NOT NULL，唯一索引 | 分类名称（单级） |
| `CreatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | |

- 无停用状态、无软删除（轻量字典；被引用时禁止删除，提示改编辑改名）。
- 实体配置 `App.Infrastructure/Persistence/Configurations/CategoryConfiguration.cs`。

### 2.2 实体 `App.Core/Entities/Product.cs` 与表 `Products`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `Code` | `string` | `varchar(32)` | NOT NULL，唯一索引 | 商品编码，创建后不可改 |
| `Name` | `string` | `varchar(50)` | NOT NULL | 商品名称 |
| `CategoryId` | `Guid` | `uuid` | NOT NULL，FK → `Categories(Id)` | 单级分类 |
| `Unit` | `string` | `varchar(10)` | NOT NULL | 计量单位（个 / 箱 / 斤…） |
| `PurchasePrice` | `decimal` | `numeric(18,2)` | NOT NULL，≥ 0 | 默认采购价 |
| `SalePrice` | `decimal` | `numeric(18,2)` | NOT NULL，≥ 0 | 默认销售价 |
| `SafetyStock` | `int` | `integer` | NOT NULL，默认 0，≥ 0 | 安全库存阈值（低库存提醒） |
| `Status` | `ProductStatus` | `smallint` | NOT NULL，默认 `1` | 启用 / 停用（停用不可被新单据选择） |
| `Remark` | `string?` | `varchar(200)` | NULL | 备注 |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 审计字段 |
| `CreatedBy` / `UpdatedBy` | `Guid?` | `uuid` | NULL | 操作人（`ICurrentUser.Id`） |

新增 `App.Core/Entities/ProductStatus.cs`：`public enum ProductStatus { Disabled = 0, Enabled = 1 }`。

**库存不在本表**：当前库存查 `Inventory`（§2.3）；列表 DTO 的库存列由仓储联查带出。

### 2.3 实体 `App.Core/Entities/Inventory.cs` 与表 `Inventory`（1:1）

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `ProductId` | `Guid` | `uuid` | NOT NULL，**唯一索引**，FK → `Products(Id)` | 与商品一对一 |
| `Quantity` | `int` | `integer` | NOT NULL，默认 0 | 当前库存（**允许为负**：仅采购作废回冲可产生，见 §5） |
| `UpdatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 最近变动时间 |

- 无软删除（商品停用不删库存行）；无 `WarehouseId`（单仓库；未来多仓库加该列并把唯一约束改为 `(WarehouseId, ProductId)`，其余结构不动）。
- **原子增减**（EF Core `ExecuteUpdateAsync` 表达，无裸 SQL，满足后端规则 §5.1）：
  - `Task IncrementAsync(Guid productId, int delta, ...)` —— `Quantity = Quantity + delta, UpdatedAt = now WHERE ProductId = @id`；
  - `Task<bool> TryDecrementAsync(Guid productId, int amount, ...)` —— `Quantity = Quantity - amount WHERE ProductId = @id AND Quantity >= amount`，返回受影响行数是否 ≥ 1（**数据库层防超卖**，并发下无需行锁）。
- 商品新增时在同一事务内 `AddAsync` 一条 `Quantity = 0` 的库存行。

### 2.4 EF Core 与迁移

- `AppDbContext` 新增 3 个 `DbSet`：`Categories`、`Products`、`Inventory`；每实体一个 `IEntityTypeConfiguration` 放 `Persistence/Configurations/`。
- 外键均不级联删除（商品只停用不删除）；`Categories → Products` 为普通 FK（分类删除由应用层先校验引用）；`Products → Inventory` 普通 FK。
- 迁移：`dotnet ef migrations add AddErpProduct -p src/App.Infrastructure -s src/App.Api`（**增量迁移**，不动既有 `InitialCreate`）。
- 无种子数据（业务数据全部经界面录入；首期库存经采购单建立，不做期初建账）。

### 2.5 字段约束单一来源（`App.Core/Entities/*FieldConstraints.cs`）

| 常量类 | 常量 | 值 |
|---|---|---|
| `CategoryFieldConstraints` | `NameMinLength` / `NameMaxLength` | 1 / 20 |
| `ProductFieldConstraints` | `CodeMinLength` / `CodeMaxLength` | 2 / 32 |
| | `CodePattern` | `^[A-Za-z0-9_-]{2,32}$` |
| | `NameMinLength` / `NameMaxLength` | 2 / 50 |
| | `UnitMaxLength` | 10 |
| | `PriceMinValue` / `PriceMaxValue` | `0m` / `9999999.99m`（采购价 / 销售价共用；后续 erp-purchase / erp-sale 的单价 / 小计 / 总额共用同常量） |
| | `QuantityMinValue` / `QuantityMaxValue` | `1` / `999999`（预留，供 erp-purchase / erp-sale 明细数量引用） |
| | `SafetyStockMinValue` / `SafetyStockMaxValue` | `0` / `999999` |
| | `RemarkMaxLength` | 200 |
| | `KeywordMaxLength` | 50（查询关键词，对齐 Code / Name 列长） |

- EF 实体配置（`HasMaxLength` / `IsRequired` / `HasColumnType` / 索引）与全部 `RequestValidator` 均引用上述常量，禁止硬编码。
- 一致性由单测守护（扩展 `FieldValidationConsistencyTests`）：EF 模型实际 `HasMaxLength` == 常量；同一字段跨用例 Validator「边界值通过 / 越界拒绝」一致；查询关键词长度不超对应列长。

## 3. 后端设计

### 3.1 仓储接口（新增，`App.Core/Abstractions/`）

`ICategoryRepository`：

| 方法 | 说明 |
|---|---|
| `Task<IReadOnlyList<Category>> GetAllAsync(...)` | 全部分类（下拉 / 筛选用，量小全量取） |
| `Task<bool> ExistsByNameAsync(string name, Guid? excludeId, ...)` | 名称存在性（大小写不敏感） |
| `Task<Category?> GetByIdAsync(Guid id, ...)` | 按 id（供编辑 / 删除） |
| `Task AddAsync(Category, ...)` / `UpdateAsync(Category, ...)` / `DeleteAsync(Guid id, ...)` | 新增 / 更新 / 删除 |
| `Task<bool> ReferencedByProductsAsync(Guid id, ...)` | 是否被商品引用（删除前校验） |

`IProductRepository`：

| 方法 | 说明 |
|---|---|
| `Task<Product?> GetByIdAsync(Guid id, ...)` | 按 id（含跟踪，供编辑 / 启停） |
| `Task<bool> ExistsByCodeAsync(string code, Guid? excludeId, ...)` | 编码存在性（大小写不敏感） |
| `Task<(IReadOnlyList<ProductListItem> Items, int Total)> GetPagedAsync(string? keyword, Guid? categoryId, ProductStatus? status, int page, int pageSize, ...)` | 分页 + 筛选；联查 `Inventory` 带出 `stockQuantity`（联查结果映射进列表项） |
| `Task<ProductDetail?> GetDetailAsync(Guid id, ...)` | 详情（联查库存 + 分类名） |
| `Task AddAsync(Product, ...)` / `UpdateAsync(Product, ...)` | 新增 / 更新 |
| `Task<IReadOnlyList<ProductPickItem>> GetPickListAsync(...)` | 开单商品选择：仅启用商品，返回 id / 编码 / 名称 / 单位 / 采购价 / 销售价 / 当前库存（全量，MVP 数据量可控） |

`IInventoryRepository`：

| 方法 | 说明 |
|---|---|
| `Task AddAsync(Inventory, ...)` | 商品新建时初始化 `Quantity = 0` 行 |
| `Task IncrementAsync(Guid productId, int delta, ...)` | 原子增加（`delta` 可为负，采购入库 / 采购作废回冲用，由 erp-purchase 消费） |
| `Task<bool> TryDecrementAsync(Guid productId, int amount, ...)` | 原子扣减且 `Quantity >= amount` 前置，返回是否成功（由 erp-sale 消费） |

- `GetPagedAsync` 联查 `Inventory` 带出 `stockQuantity`；低库存标记 `isBelowSafetyStock` 由 Handler 计算（`safetyStock > 0 && stockQuantity < safetyStock`，阈值为 0 不提醒，避免零库存商品全量标红），不在仓储内计算。
- 单一仓储写由仓储自身 `SaveChangesAsync` 保证；**跨仓储写**（商品 + 库存初始化）必须用 `IUnitOfWork` 包成同一事务：`BeginTransactionAsync` → 各仓储写 → `CommitAsync`，异常 `RollbackAsync` 后重抛（后端规则 §4.4）。
- 审计字段统一由 Handler 经 `ICurrentUser` 获取后随实体传入，仓储不感知当前用户。

### 3.2 错误码（追加到 `App.Core/Errors/ErrorCode.cs`）

| code | 常量 | 含义 |
|---:|---|---|
| 40101 | `ProductCodeExists` | 商品编码已存在 |
| 40105 | `CategoryNameExists` | 商品分类名称已存在 |
| 40106 | `CategoryInUse` | 分类已被商品引用，禁止删除 |
| 40107 | `ProductDisabled` | 商品已停用，不可用于开单（本规格开单选择接口由前端过滤，保留错误码常量供 erp-purchase / erp-sale 使用） |

> `40400 NotFound` / `40000 Validation` 复用全局。其余进销存错误码（40102 / 40103 / 40104 / 40108 / 40109 / 40110）由 erp-partner / erp-purchase / erp-sale 各自定义。

### 3.3 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

| 接口 | 方法 | 用例目录 | `data` 响应 | 错误码 |
|---|---|---|---|---|
| `/api/products` | GET | `Products/GetProducts` | `PagedResult<ProductListItemDto>` | 40000 |
| `/api/products` | POST | `Products/CreateProduct` | `ProductDetailDto` | 40000 / 40101 |
| `/api/products/{id:guid}` | GET | `Products/GetProductById` | `ProductDetailDto` | 40400 |
| `/api/products/{id:guid}` | PUT | `Products/UpdateProduct` | `ProductDetailDto` | 40000 / 40400 |
| `/api/products/{id:guid}/status` | PUT | `Products/UpdateProductStatus` | `ProductDetailDto` | 40000 / 40400 |
| `/api/products/pick` | GET | `Products/GetProductPickList` | `IReadOnlyList<ProductPickDto>` | 40000 |
| `/api/categories` | GET | `Categories/GetCategories` | `IReadOnlyList<CategoryDto>` | 40000 |
| `/api/categories` | POST | `Categories/CreateCategory` | `CategoryDto` | 40000 / 40105 |
| `/api/categories/{id:guid}` | PUT | `Categories/UpdateCategory` | `CategoryDto` | 40000 / 40105 / 40400 |
| `/api/categories/{id:guid}` | DELETE | `Categories/DeleteCategory` | `null` | 40106 / 40400 |

路由注意：`/api/products/pick` 为固定段，置于 `{id:guid}` 之前注册（`{id:guid}` 约束本身已排除 `pick`，双保险）。

### 3.4 关键用例流程（Handler）

**CreateProduct**：
1. `ExistsByCodeAsync(code, null)` 为真 → `40101`。
2. 校验 `CategoryId` 存在（不存在 → `40400`）。
3. 组 `Product`（`Status=Enabled`、审计字段取 `ICurrentUser`）+ `Inventory { Quantity = 0 }`。
4. `IUnitOfWork`：`BeginTransactionAsync` → `IProductRepository.AddAsync` + `IInventoryRepository.AddAsync` → `CommitAsync`。

**UpdateProduct**：取商品（不存在 → `40400`）→ 更新名称 / 分类 / 单位 / 价格 / 安全阈值 / 备注（**不触碰 `Code`**，`Code` 为不可改字段；可选字段遵循 `AGENTS.md` §4.5 全量覆盖语义）→ 更新审计。

**UpdateProductStatus**：取商品（不存在 → `40400`）→ 置 `Status` → 更新审计。

**CreateCategory**：`ExistsByNameAsync(name, null)` 为真 → `40105`；否则新增。
**UpdateCategory**：`GetByIdAsync`（不存在 → `40400`）→ `ExistsByNameAsync(name, id)` 为真 → `40105`；否则更新。
**DeleteCategory**：`GetByIdAsync`（不存在 → `40400`）→ `ReferencedByProductsAsync` 为真 → `40106`；否则删除。

**GetProducts**：仓储分页联查带出 `stockQuantity` → Handler 映射 DTO 并计算 `isBelowSafetyStock = safetyStock > 0 && stockQuantity < safetyStock`。

**GetProductPickList**：仓储仅取启用商品（联查当前库存）→ Handler 映射 DTO。

### 3.5 校验规则（FluentValidation，仅格式层，引用 §2.5 常量）

| 请求 | 规则 |
|---|---|
| `CreateProductRequest` | `code` 必填 2–32、`CodePattern`；`name` 必填 2–50；`categoryId` 必填；`unit` 必填 ≤10；`purchasePrice` / `salePrice` 0–9999999.99；`safetyStock` 0–999999；`remark` ≤200 |
| `UpdateProductRequest` | 同 create，去掉 `code`（不可改） |
| `CreateCategoryRequest` / `UpdateCategoryRequest` | `name` 必填 1–20 |
| `GetProductsRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 50（`KeywordMaxLength`）；`categoryId` / `status` 可空或合法值 |

- 存在性 / 唯一性等业务约束一律在 Handler 判断（后端规则 §4.1）。

### 3.6 Swagger

- **不分组**（唯一来源见 `specs/003-api-swagger/design.md`）：新增接口按现有方式出现在单文档 Swagger 中。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── product.ts            # 商品 + 分类接口层（类型 + 请求函数）
└── views/
    └── ProductManagement/
        ├── ProductsView.vue      # 商品列表页（含库存列）
        └── ProductFormDrawer.vue # 新增/编辑抽屉
```

> **演进（erp-category）**：分类维护已迁出为独立页面 `CategoryManagement/`（见 `specs/017-erp-category/`），商品抽屉的「新建分类」改为就地行内输入；分类维护唯一入口为侧边菜单「进销存 → 分类管理」。

- 商品字段 ≤ 8，新增 / 编辑用**抽屉**（`a-drawer`，`unmount-on-close`，底部自定义操作栏）；分类维护不在本域（见 `specs/017-erp-category/`）。
- 商品详情：列表操作列「详情」复用 `ProductFormDrawer` 的查看态（disabled）；字段虽多但复用表单组件展示，不另设独立详情页（与 `user-management` 的详情抽屉惯例一致，design 记录该决策）。

### 4.2 接口层

- `src/api/product.ts`：TS 类型与后端 DTO（camelCase）一一对应；请求统一经 `src/api/request.ts`（约定见前端规则 §3）。
- 金额字段类型：`number`（后端 `numeric(18,2)` JSON 序列化为数字）；展示统一 `toFixed(2)`。
- 分类下拉数据源 `GET /api/categories`（全量，量小）；开单选择接口 `GET /api/products/pick` 本规格仅交付接口，由 erp-purchase / erp-sale 页面消费。

### 4.3 路由与菜单

`src/router/index.ts` 新增（均 `meta.requiresAuth: true`）：

| path | name | 组件 |
|---|---|---|
| `products` | `products` | `ProductsView` |

`AppLayout.vue` 侧边菜单新增子项「商品管理」`products`。**菜单分组结构唯一来源**见 `specs/025-erp-report/design.md` §0.2（`025` 已把原「进销存」单分组重构为多顶级分组）。

### 4.4 页面交互

**商品列表 `ProductsView.vue`**（参照 `UsersView.vue`）：
- 筛选行：关键词（编码 / 名称）+ 分类下拉（全部 / 各分类）+ 状态下拉 + 搜索 / 重置。
- 操作行：新增（primary，抽屉）、刷新、列设置。
- 表格列：序号、编码、名称、分类、单位、采购价、销售价、**库存**（低于安全阈值标红 + `a-tag warning`「低库存」）、安全库存、状态（`a-tag` 绿 / 红）、创建时间、操作列（≤3 平铺：编辑 / 停用或启用（popconfirm）/ 详情）。
- 服务端分页，条件变化回第 1 页。

**商品抽屉 `ProductFormDrawer.vue`**：
- 新增：编码、名称、分类（下拉 + 「新建分类」就地行内输入，选中回填）、单位、采购价、销售价、安全库存、备注；编辑：同上去掉编码（只读展示）。
- 金额 `a-input-number :min="0" :max="9999999.99" :precision="2"`；安全库存 `:min="0" :precision="0"`。
- 打开时先 `Object.assign(form, emptyForm())` 重置（同用户抽屉约定，防数据串台）；提交 `submitting` + 防重入。

**分类弹窗 `CategoryManagerModal.vue`**：
- `a-modal` 内小型列表（名称 / 创建时间 / 操作）+ 顶部行内新增输入 + 保存；行内编辑（输入框 + 确认）；删除 `a-popconfirm`（有引用时后端返回 40106，统一错误提示）。

### 4.5 按钮 loading（遵循前端规则 §4.6）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 列表查询 | `loading` | 搜索 / 翻页 + 表格 |
| 商品抽屉提交 | `submitting` | 提交按钮 |
| 商品启停（行内） | `togglingId` | `:loading="togglingId === row.id"` |
| 分类新增 / 编辑 / 删除 | `categorySubmitting` / `deletingCategoryId` | 弹窗按钮 / popconfirm |

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| 库存独立表 1:1 | `Inventory` + `ProductId` 唯一 | 库存读写与商品档案解耦；未来多仓库仅需加 `WarehouseId` 改唯一约束 |
| 防超卖能力 | `TryDecrementAsync` 条件更新（`WHERE Quantity >= amount`） | 数据库行锁内原子完成判断 + 扣减，无竞态窗口；不引入显式行锁 / 乐观版本号；由 erp-sale 消费 |
| 采购作废回冲允许冲负 | `IncrementAsync(delta 负值)` 不设下限 | 采购入库后商品可能已被卖光，回冲必须可执行；库存为负作为数据异常展示（库存页标红可见，由 erp-inventory-query 呈现） |
| 商品详情复用表单抽屉 | 抽屉 disabled 态查看，不设独立详情页 | 与 user-management 详情惯例一致，MVP 阶段字段展示够用；后续有审计需求再升级 |
| 分类无停用、不可删除被引用项 | 被引用 → `40106` 拒绝删除 | 字典类数据，改名（编辑）即可解决展示问题，停用状态属过度设计 |
| 商品停用不删除 | 停用不可被新单据选择，保留历史引用 | 单据引用商品档案，删除会破坏审计轨迹 |
| 无 RBAC | 登录即可见商品管理菜单 | 用户确认本期不做权限；后续权限模块统一接入 |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储接口；`TestCurrentUser`（`ICurrentUser`）同 `user-management` 测试约定；时间用固定 `DateTimeOffset` 入参或注入时钟，不读 `DateTime.Now`（Handler 时间来源统一为仓储传入的 `utcNow` 参数，同用户模块）。

### 6.1 商品

- **GetProducts**：无筛选 / 关键词（命中编码或名称）/ 分类 / 状态组合筛选 → 正确传参仓储并映射 DTO（含 `stockQuantity` / `isBelowSafetyStock`：阈值 0 → false；阈值 > 0 且库存 < 阈值 → true；库存 = 阈值 → false）。
- **CreateProduct**：成功（编码唯一路径，断言 `Product` + `Inventory(Quantity=0)` 同时入 `IUnitOfWork` 提交）；编码已存在 → `40101`；分类不存在 → `40400`。
- **UpdateProduct**：成功（断言不改 `Code`）；不存在 → `40400`。
- **UpdateProductStatus**：成功；不存在 → `40400`；`40000`（非法 status 由 Validator，不在 Handler 测）。
- **GetProductPickList**：仅返回启用商品（仓储已过滤，Handler 映射断言含库存字段）。
- **GetProductById**：存在 / 不存在（`40400`）。

### 6.2 分类

- **CreateCategory / UpdateCategory**：成功；重名 → `40105`；更新时 `excludeId` 传自身（同名不冲突）。
- **DeleteCategory**：无引用成功；有引用 → `40106`；不存在 → `40400`。
- **GetCategories**：映射断言。

### 6.3 字段约束一致性（扩展 `FieldValidationConsistencyTests`）

- 商品：EF `HasMaxLength`（Code 32 / Name 50 / Unit 10 / Remark 200）== `ProductFieldConstraints`；Validator 边界（code 32 字符通过 / 33 拒绝；price 9999999.99 通过 / 10000000 拒绝；safetyStock 999999 通过 / 1000000 拒绝）。
- 分类：Name 20 通过 / 21 拒绝。
- 跨用例一致性：`keyword` 在商品列表 Validator 与 `KeywordMaxLength` 常量一致（≤ Code / Name 列长）。
