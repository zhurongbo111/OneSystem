---
created: 2026-09-20
updated: 2026-09-20
---

# 设计规格：计量单位（erp-uom）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `017-erp-category`（轻量字典域模板）为结构参照；**改造 `012-erp-product`** 的单位字段。
> 权限机制复用 `028`；本规格只续行权限点。

## 0. 约定正文（唯一事实源）

### 0.1 唯一性与删除保护

| 对象 | 字段 | 唯一范围 | 冲突错误码 |
|---|---|---|---|
| 单位 | `Code` | 全局 | `40163` |
| 单位 | `Name` | 全局 | `40164` |

| 动作 | 前置检查 | 错误码 |
|---|---|---|
| 删除单位 | 无商品引用 | `40165` |

### 0.2 商品单位引用口径

- **单位是字典、商品引用**：`Product.UnitId`（NOT NULL）指向 `Units`；`Product.Unit` 保留为**名称快照**（`varchar(20)`），由 Handler 在写入时从单位取名字填入，列表 / 出参免 join。
- **单据 `Unit` 快照不变**：出入库 / 订单 / 退货明细的 `Unit` 仍为下单时商品单位名的快照（取值来源变为字典，列与语义不变）。
- 单位改名不影响既有单据快照；商品的 `Unit` 快照随商品下次保存刷新。

### 0.3 菜单归属（在 `025` §0.2 表续行）

「基础档案（`basedata`）」分组续行一子项：

| 顶级分组 | 子项（key） | 引入规格 |
|---|---|---|
| 基础档案（`basedata`） | 计量单位（`units`） | `035` |

### 0.4 权限点（在 `028` §0.2 表续行）

| 域 | key 前缀 | 权限点（动作） | 对应接口 / 页面 |
|---|---|---|---|
| 计量单位 | `units` | `view` / `create` / `update` / `delete` / `status` | `/api/units*`、`/units` |

## 1. 总体设计

```
计量单位（前端 /units，域目录 UnitManagement/）
  → UnitsController
    → App.Core/Features/Units/<Action>/*RequestHandler
      → IUnitRepository（App.Core）→ EF Core 实现（App.Infrastructure）
        → PostgreSQL（Units）

商品单位引用（012 改造）
  CreateProduct / UpdateProduct → 校验 UnitId 存在 → 填 Unit 快照 → 落库
```

## 2. 数据模型

> 时间字段统一 `DateTimeOffset` → `timestamptz`；枚举 `smallint`。

### 2.1 实体 `App.Core/Entities/Unit.cs` 与表 `Units`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `Code` | `string` | `varchar(20)` | NOT NULL，唯一索引 | 单位编码（如 `PCS`、`BOX`） |
| `Name` | `string` | `varchar(20)` | NOT NULL，唯一索引 | 单位名称（如「个」「箱」） |
| `Status` | `UnitStatus` | `smallint` | NOT NULL，默认 `Enabled` | `UnitStatus { Disabled = 0, Enabled = 1 }` |
| `Remark` | `string?` | `varchar(200)` | NULL | |
| 审计 | | | | `CreatedAt` / `UpdatedAt` / `CreatedBy` / `UpdatedBy` |

### 2.2 商品表改造（`012`）

| 项 | 变化 |
|---|---|
| 列 | `Products` 增 `UnitId`（`uuid`，NOT NULL，FK → `Units(Id)`，索引）；`Unit` 列长 `varchar(10)` → `varchar(20)`（名称快照） |
| 实体 | `Product` 增 `UnitId`；`Unit` 语义改为快照 |
| 请求 | `CreateProductRequest` / `UpdateProductRequest` 的 `unit`（字符串）→ `unitId`（GUID，必填） |
| 出参 | 商品列表 / 详情出参保持 `unit`（名称快照），新增可选 `unitId` |
| 校验 | 单位存在性在 Handler（不存在 → `40400`） |

### 2.3 字段约束常量类

| 常量类 | 常量 |
|---|---|
| `UnitFieldConstraints` | `CodeMaxLength = 20` / `NameMaxLength = 20` / `RemarkMaxLength = 200` |

- `ProductFieldConstraints.UnitMaxLength` 由 `10` 调整为 `20`（与 `UnitFieldConstraints.NameMaxLength` 一致，由一致性单测守护）。

### 2.4 迁移与种子（含数据回填）

- 迁移：`dotnet ef migrations add AddErpUom -p src/App.Infrastructure -s src/App.Api`：
  1. 建 `Units` 表；
  2. `Products` 增 `UnitId`（先可空，回填后不予改为 NOT NULL 的迁移内处理见下）；
  3. **数据回填**（`migrationBuilder.Sql`，仅迁移内允许）：
     - 从 `Products.Unit` 去重生成单位记录（`Code` 用 `U` + 序号，`Name` = 原文本）；
     - `UPDATE "Products" SET "UnitId" = <对应单位> WHERE "Unit" = <原文本>`；
     - 无法解析（空 / 异常文本）→ 归入兜底单位「个」；
  4. 回填完成后 `UnitId` 置 NOT NULL。
- 无业务种子（单位由用户维护 + 迁移回填生成）。

## 3. 后端设计

### 3.1 仓储接口（`App.Core/Abstractions/`）

| 接口 / 方法 | 说明 |
|---|---|
| `IUnitRepository.GetPagedAsync(...)` / `GetByIdAsync` / `ExistsByCodeAsync` / `ExistsByNameAsync` / `AddAsync` / `UpdateAsync` / `DeleteAsync` / `GetPickListAsync()` | |
| `IUnitRepository.ReferencedByProductsAsync(id)` | 删除保护 |
| `IProductRepository`（`012` 改造） | 写路径改为接收 `UnitId`；`GetByIdAsync` 等出参含 `UnitId` |

读模型：`UnitListItem`、`UnitPickItem`。

### 3.2 错误码（`ErrorCode.cs`，从 `40163` 起）

| code | 常量 | 含义 |
|---:|---|---|
| 40163 | `UnitCodeExists` | 单位编码已存在 |
| 40164 | `UnitNameExists` | 单位名称已存在 |
| 40165 | `UnitInUse` | 单位已被商品引用，禁止删除 |

> 下一个可用业务码 → `40166`（`ROADMAP` §6 顶部同步）。

### 3.3 用例与接口

| 接口 | 方法 | 用例目录 | `data` | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/units` | GET | `Units/GetUnits` | `PagedResult<UnitListItemDto>` | `units.view` / 40000 |
| `/api/units` | POST | `Units/CreateUnit` | `UnitDetailDto` | `units.create` / 40000 / 40163 / 40164 |
| `/api/units/{id:guid}` | GET | `Units/GetUnitById` | `UnitDetailDto` | `units.view` / 40400 |
| `/api/units/{id:guid}` | PUT | `Units/UpdateUnit` | `UnitDetailDto` | `units.update` / 40000 / 40163 / 40164 / 40400 |
| `/api/units/{id:guid}` | DELETE | `Units/DeleteUnit` | `null` | `units.delete` / 40165 / 40400 |
| `/api/units/{id:guid}/status` | PUT | `Units/UpdateUnitStatus` | `UnitDetailDto` | `units.status` / 40400 |
| `/api/units/picks` | GET | `Units/GetUnitPicks` | `IReadOnlyList<UnitPickDto>` | `units.view` |

- 另有 `012` 的 `CreateProduct` / `UpdateProduct` 改造（请求 `unitId`，Handler 校验 + 填快照）。

### 3.4 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `CreateUnitRequest` / `UpdateUnitRequest` | `code` 必填 1–20；`name` 必填 1–20；`remark` ≤ 200 |
| `GetUnitsRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 20 |
| `CreateProductRequest` / `UpdateProductRequest`（`012` 改造） | `unitId` 必填 GUID（替换原 `unit` 字符串校验） |

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── unit.ts                    # 单位接口层
└── views/
    └── UnitManagement/
        ├── UnitsView.vue              # 单位列表
        └── UnitFormDrawer.vue         # 单位新增 / 编辑抽屉
```

- `views/ProductManagement/ProductFormDrawer.vue` 改造：单位字段从 `a-input` 改为 `a-select`（数据源 `getUnitPicks`）。

### 4.2 路由与菜单

| path | name | 组件 |
|---|---|---|
| `units` | `units` | `UnitsView` |

- 「基础档案」分组续行「计量单位」；`meta.permission = 'units.view'`。

### 4.3 页面交互

- **`UnitsView.vue`**：标准列表（筛选：关键词 / 状态）；列：编码、名称、状态、备注、创建时间、操作列（编辑 / 启停 / 删除）。
- **`UnitFormDrawer.vue`**：编码 / 名称 / 状态 / 备注。
- **`ProductFormDrawer.vue`（`012` 改造）**：单位 `a-select`（启用单位），提交 `unitId`。

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 |
|---|---|---|
| `Unit` 保留为快照 | `UnitId` + `Unit` 名称列 | 列表免 join；单位改名不回溯历史商品展示（下次保存刷新） |
| 单位改名不回写历史商品 | 快照语义 | 商品展示是"当时单位名"，与单据快照原则一致；如需同步可批量刷（不做） |
| 迁移按文本去重建单位 | 数据回填 | 平滑升级，无数据丢失；异常文本归兜底「个」 |
| 多单位换算不做 | 范围外 | 需改库存 / 单据数量语义，风险与工作量另立规格 |

## 6. 单元测试设计

- **单位**：`CreateUnit` / `UpdateUnit` 编码 `40163`、名称 `40164`；`DeleteUnit` 被引用 `40165` / 成功；筛选分页；`GetUnitPicks` 仅启用。
- **商品改造**：`CreateProduct` / `UpdateProduct` 的 `unitId` 存在性（`40400`）与快照填写；出参 `unit` 为名称。
- **迁移回填**（集成）：既有商品 `Unit` 文本去重生成单位并回填（可用测试数据集）。
- **字段约束一致性**：`UnitFieldConstraints.NameMaxLength` == EF 列长；`ProductFieldConstraints.UnitMaxLength == 20`。
- **清单守卫**：7 个端点纳入 `028` 既有守卫。
