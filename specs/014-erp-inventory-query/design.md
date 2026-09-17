---
created: 2026-09-13
updated: 2026-09-17
---

# 设计规格：库存查询（erp-inventory-query）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `user-management` 为结构参照。
> 本规格消费 erp-product 建立的 `Inventory` / `Products` / `Categories` 表与 `IInventoryRepository`，**不引入新表 / 新实体 / 新迁移**。

## 1. 总体设计

```
库存查询（前端 /inventory）
  → InventoryController
    → App.Core/Features/Inventory/GetInventory/*RequestHandler
      → IInventoryRepository.GetPagedAsync（App.Core）→ InventoryRepository（App.Infrastructure，EF Core）
        → AppDbContext → PostgreSQL（联查 Inventory / Products / Categories）
```

核心原则：

- **纯只读**：本规格只查询，不写 `Inventory`（库存写入由 erp-purchase / erp-sale 驱动）。
- **仅启用商品**：联查时过滤 `Products.Status = Enabled`，停用商品即使库存非 0 也不展示。
- **低库存判定**在 Handler 计算（`safetyStock > 0 && stockQuantity < safetyStock`），仓储只负责联查出 `safetyStock` / `stockQuantity` 原始值，与 erp-product 商品列表逻辑一致。

## 2. 数据模型

> 本规格**不新增表 / 实体 / 迁移**。复用 erp-product 已建立的：
>
> - `Inventory`（`ProductId` 唯一、`Quantity`、`UpdatedAt`）——见 erp-product design §2.3；
> - `Products`（`Code` / `Name` / `CategoryId` / `Unit` / `SafetyStock` / `Status`）——见 erp-product design §2.2；
> - `Categories`（`Name`）——见 erp-product design §2.1。
>
> 联查结果 DTO 为本规格新增（放 `Features/Inventory/` 共享出参，非实体）。

## 3. 后端设计

### 3.1 仓储扩展（`App.Core/Abstractions/IInventoryRepository`）

在 erp-product 已定义的 `IInventoryRepository` 上**追加**一个只读方法（接口追加成员，实现类补实现，不改动既有方法签名）：

| 方法 | 说明 |
|---|---|
| `Task<(IReadOnlyList<InventoryItem> Items, int Total)> GetPagedAsync(string? keyword, Guid? categoryId, int page, int pageSize, ...)` | 库存查询：联查 `Inventory` / `Products` / `Categories`，**仅启用商品**（`Products.Status = Enabled`）；keyword 模糊匹配编码 / 名称；categoryId 精确匹配；`ORDER BY Products.Code ASC` |

- `InventoryItem` 为仓储出参模型（App.Core）：`ProductId` / `Code` / `Name` / `CategoryName` / `Unit` / `Quantity` / `SafetyStock` / `UpdatedAt`。
- 联查用 EF Core 投影（`Join` / 子查询均可，以可读为先），不写裸 SQL。

### 3.2 错误码

> 无新增错误码。`40000 Validation` / `40400 NotFound` 复用全局（本规格仅一个只读列表接口，正常路径无业务失败）。

### 3.3 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

| 接口 | 方法 | 用例目录 | `data` 响应 | 错误码 |
|---|---|---|---|---|
| `/api/inventory` | GET | `Inventory/GetInventory` | `PagedResult<InventoryItemDto>` | 40000 |

### 3.4 用例流程（Handler）

**GetInventory**：
1. 仓储 `GetPagedAsync(keyword, categoryId, page, pageSize)`（启用商品过滤 + 联查在仓储内完成）。
2. Handler 映射 `InventoryItemDto`，计算 `isBelowSafetyStock = safetyStock > 0 && stockQuantity < safetyStock`（阈值为 0 不提醒，避免零库存商品全量标红）。

### 3.5 校验规则（FluentValidation，仅格式层）

| 请求 | 规则 |
|---|---|
| `GetInventoryRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 50（引用 `ProductFieldConstraints.KeywordMaxLength`，对齐 Code / Name 列长）；`categoryId` 可空 |

### 3.6 Swagger

- **不分组**（用户已确认）：维持现有单文档 Swagger，`GET /api/inventory` 按现有方式正常出现在文档中，不使用 `ApiExplorerSettings.Group`。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── inventory.ts            # 库存查询接口层
└── views/
    └── InventoryManagement/
        └── InventoryView.vue   # 库存查询（只读列表）
```

### 4.2 接口层

- `src/api/inventory.ts`：TS 类型与后端 DTO（camelCase）一一对应；函数经 `src/api/request.ts` 统一封装（解包 `data`、40100 处理）。
- 分类下拉数据源复用 `src/api/product.ts` 的 `getCategories`（全量，量小）。

### 4.3 路由与菜单

`src/router/index.ts` 新增（均 `meta.requiresAuth: true`）：

| path | name | 组件 |
|---|---|---|
| `inventory` | `inventory` | `InventoryView` |

`AppLayout.vue` 侧边菜单「进销存」分组追加子项「库存查询」`inventory`（分组与既有子项由 erp-product 等创建）。

### 4.4 页面交互

**库存查询 `InventoryView.vue`**（只读，参照 `UsersView.vue` 列表结构）：
- 筛选行：关键词（编码 / 名称）+ 分类下拉 + 搜索 / 重置。
- 表格列：序号、编码、名称、分类、单位、**当前库存**（低于阈值标红 + `a-tag warning`「低于安全库存」）、安全阈值、最近变动时间；按编码升序（后端排序）；服务端分页；**操作列无**（纯只读）。
- 库存为 0 且阈值 > 0 的行同样标红提醒（缺货可见）。

> **演进（erp-stock-movement）**：操作列从无 → 新增 1 个只读「流水」按钮（Tabler `IconListDetails`）→ 库存流水页（路由 `stock-movements`）并带 `productId` 预置筛选。库存写入仍只由单据 / 盘点驱动，新增入口仅为只读下钻，不开放手工改库存；现行为准见 `specs/019-erp-stock-movement/` §4.4。

### 4.5 按钮 loading（遵循前端规则 §4.6）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 列表查询 | `loading` | 搜索 / 翻页 + 表格 |

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| 不新增表 / 实体 | 复用 erp-product 的 `Inventory` + 联查 | 库存数据单一来源，查询视图不做物化 |
| 仅展示启用商品 | 联查过滤 `Status = Enabled` | 停用商品已从业务流移除，库存视图不暴露，减少干扰；库存行仍保留（单据回冲需要） |
| 低库存判定在 Handler | `safetyStock > 0 && stockQuantity < safetyStock` | 与 erp-product 商品列表逻辑一致（同源同规则，防分叉）；阈值为 0 不提醒 |
| 编码升序固定排序 | 后端 `ORDER BY Code` | 库存盘点习惯按编码定位，不允许前端自定义排序（MVP 最简） |
| 纯只读无操作列 | 不提供行内操作 | 库存写入统一由单据驱动，杜绝手工改库存绕过单据审计 |

> 演进注记：`erp-stock-movement`（`specs/019-erp-stock-movement/`）已为库存页新增只读「流水」下钻入口，本行决策修订为「无写操作、允许只读下钻」，其余不变。

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储接口；时间用固定 `DateTimeOffset` 入参或注入时钟，不读 `DateTime.Now`（同用户模块约定）。

- **GetInventory**：关键词 / 分类筛选正确传参仓储（`page` / `pageSize` / `keyword` / `categoryId` 断言）；启用商品过滤在仓储层（Handler 不重复过滤，断言透传）；`isBelowSafetyStock` 映射三态（阈值 0 → false；库存 = 阈值 → false；阈值 > 0 且库存 < 阈值 → true）；分页 `total` 透传。
- **GetInventoryRequest Validator**：`page` ≥ 1 / `pageSize` 1–100 / `keyword` ≤ 50 边界通过 / 越界拒绝（引用 `ProductFieldConstraints.KeywordMaxLength`，纳入 `FieldValidationConsistencyTests` 跨用例一致性：keyword 长度不超 Code / Name 列长）。
