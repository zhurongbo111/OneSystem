---
created: 2026-10-07
updated: 2026-10-07
---

# 设计规格：主数据编码规则（erp-code-rule）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织；本规格新增 1 张配置表 + 1 个生成能力，并**改造** `012` 的商品创建（`code` 可空）。
> 商品实体 / 错误码 `40101` 见 `specs/012-erp-product/design.md` §2 / §3.2。

## 0. 约定正文（唯一事实源）

### 0.1 编码组成与格式

`编码 = 前缀 + 日期段 + 序号（补零）`

| 段 | 取值 | 说明 |
|---|---|---|
| 前缀 | 0–10 位，`^[A-Za-z0-9_-]*$`，可空 | 不含日期与序号语义 |
| 日期段 | `None`（空）/ `Day`（`yyyyMMdd`）/ `Month`（`yyyyMM`） | 按**基准日**格式化 |
| 序号 | 从 `1` 起，左补零到 `SerialLength` 位（3–8） | 见 §0.2 |

- **生成格式**：如 前缀 `A` + `Day` + 4 位 → `A202610070001`；前缀空 + `None` + 3 位 → `001`。
- **总长上限**：10（前缀）+ 8（日期段）+ 8（序号）= 26 ≤ 商品 `Code` 列长 32（`012` §2.1）。

### 0.2 序号自增与重置

| 项 | 规则 |
|---|---|
| 自增维度 | 按 **`(ResourceType, 前缀, 日期段值)`**；`日期段值` = 基准日按 `DatePattern` 格式化 |
| 重置 | 日期段值变化（跨日 / 跨月）→ 序号从 `1` 重新开始 |
| 状态 | 规则行存 `LastKey`（上次日期段值）+ `LastSerial`；`LastKey != 当前值` 时序号取 `1` 并更新 `LastKey` |
| 并发 | 自增在**同一事务内**对规则行加锁（`SELECT ... FOR UPDATE`）完成，保证不重号 |
| 基准日 | 由 Handler 传入（`DateOnly`，可测性），仓储 / 生成器不读系统时间 |

### 0.3 唯一性与冲突

| 对象 | 字段 | 规则 | 冲突错误码 |
|---|---|---|---|
| 商品 | `Code` | 生成后仍走 `ExistsByCodeAsync`（大小写不敏感，`012`） | `40101` |
| 编码规则 | `ResourceType` | 唯一（每资源一条） | — |

- 生成与落库同事务；生成值若已存在（人工填过同名）→ 自增重试 1 次，仍冲突 → `40182`。

### 0.4 商品创建接入语义

| `code` 入参 | 规则已配置 | 结果 |
|---|---|---|
| 留空 | 是 | **自动生成**（§0.1） |
| 留空 | 否 | `40181`（编码规则未配置） |
| 非空 | 是 / 否 | 用填值（`012` 原行为不变，唯一性 `40101`） |

### 0.5 权限点（在 `028` §0.2 表续行）

| 域 | key 前缀 | 权限点（动作） | 对应接口 / 页面 |
|---|---|---|---|
| 编码规则 | `codeRules` | `view` / `update` | `/api/code-rules*`、`/code-rules` |

### 0.6 菜单归属（在 `025` §0.2 表续行）

「系统（`system`）」分组续行一子项：

| 顶级分组 | 子项（key） | 引入规格 |
|---|---|---|
| 系统（`system`） | 编码规则（`codeRules`） | `050` |

## 1. 总体设计

```
编码规则（前端 /code-rules，域目录 CodeRuleManagement/）
  → CodeRulesController
    → Features/CodeRules/GetCodeRules | SaveCodeRule
      → ICodeRuleRepository → PostgreSQL（CodeRules）

编码生成（商品创建时）
  Products/CreateProduct
    → code 为空 且 规则已配置
      → ICodeGenerator.GenerateAsync(Product, today)
        → ICodeRuleRepository.ReserveSerialAsync（事务 + 行锁，§0.2）
      → 组 Product 落库（012 既有事务）
```

核心原则：

- **生成是能力、不是硬编码**：`ICodeGenerator` 按资源类型生成，资源类型枚举续行即可接入新资源（本期仅商品）。
- **可测性**：基准日入参注入；生成器不读系统时间。
- **不破坏既有用法**：`code` 填值路径与升级前完全一致；仅"留空"是新增语义。
- **并发由库保证**：序号预留走行锁，不依赖应用层重试（重试仅兜底撞码）。

## 2. 数据模型

### 2.1 实体 `App.Core/Entities/CodeRule.cs` 与表 `CodeRules`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `ResourceType` | `CodeResourceType` | `smallint` | NOT NULL，**唯一索引** | 首期仅 `Product` |
| `Prefix` | `string?` | `varchar(10)` | NULL | 前缀（可空） |
| `DatePattern` | `CodeDatePattern` | `smallint` | NOT NULL，默认 `None` | 日期段 |
| `SerialLength` | `int` | `integer` | NOT NULL，默认 `4` | 3–8 |
| `LastKey` | `string?` | `varchar(10)` | NULL | 上次日期段值（内部状态） |
| `LastSerial` | `int` | `integer` | NOT NULL，默认 `0` | 上次序号（内部状态） |
| 审计 | | | | `CreatedAt` / `UpdatedAt` / `CreatedBy` / `UpdatedBy` |

### 2.2 枚举（`App.Core/Entities/`）

| 枚举 | 取值 |
|---|---|
| `CodeResourceType` | `Product = 0`（续行扩展位） |
| `CodeDatePattern` | `None = 0` / `Day = 1`（`yyyyMMdd`）/ `Month = 2`（`yyyyMM`） |

### 2.3 字段约束常量类

| 常量类 | 常量 |
|---|---|
| `CodeRuleFieldConstraints` | `PrefixMaxLength = 10` / `PrefixPattern = ^[A-Za-z0-9_-]*$` / `SerialLengthMinValue = 3` / `SerialLengthMaxValue = 8` |

### 2.4 迁移与种子

- 迁移：`dotnet ef migrations add AddErpCodeRule -p src/App.Infrastructure -s src/App.Api`（建 1 表 + 唯一索引）。
- 种子：**不预置**规则（未配置时商品编码仍必填，行为兼容）。

## 3. 后端设计

### 3.1 仓储与生成器（`App.Core/Abstractions/` + `App.Infrastructure/`）

| 类型 | 位置 | 说明 |
|---|---|---|
| `ICodeRuleRepository.GetAllAsync` / `GetByResourceAsync` | `App.Core/Abstractions/` | 规则读取 |
| `ICodeRuleRepository.UpsertAsync(rule, ...)` | 同上 | 按资源类型保存 |
| `ICodeRuleRepository.ReserveSerialAsync(resourceType, key, today, ...)` | 同上 | **事务内行锁**自增：`LastKey != key` → 序号 `1` 并写 `LastKey`；否则 `LastSerial + 1`；返回新序号 |
| `ICodeGenerator` / `CodeGenerator` | `App.Core/Abstractions/` + `App.Infrastructure/` | `GenerateAsync(CodeResourceType, DateOnly today, ...)`：取规则（无 → `40181`）→ 组前缀 / 日期段 → `ReserveSerialAsync` → 补零拼接 |

- 读模型 / DTO：`CodeRuleItem`（`ResourceType` / `Prefix` / `DatePattern` / `SerialLength`）；`CodeRuleDtoMapper`。

### 3.2 错误码（`ErrorCode.cs`，从 `40181` 起）

| code | 常量 | 含义 |
|---:|---|---|
| 40181 | `CodeRuleNotConfigured` | 该资源未配置编码规则（商品编码留空时） |
| 40182 | `CodeGenerateConflict` | 自动生成编码冲突（重试后仍撞码） |

> 下一个可用业务码 → `40183`（`ROADMAP` §6 顶部同步）。

### 3.3 用例与接口

| 接口 | 方法 | 用例目录 | `data` | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/code-rules` | GET | `CodeRules/GetCodeRules` | `IReadOnlyList<CodeRuleItemDto>` | `codeRules.view` |
| `/api/code-rules/{resourceType:int}` | PUT | `CodeRules/SaveCodeRule` | `CodeRuleItemDto` | `codeRules.update` / 40000 / 40400 |
| `/api/products`（改造） | POST | `Products/CreateProduct` | `ProductDetailDto` | `products.create` / 40000 / **40101 / 40181 / 40182** / 40400 |

### 3.4 关键用例流程（Handler）

- **SaveCodeRule**：资源类型合法 → `UpsertAsync`（不重置 `LastKey` / `LastSerial`，避免改配置后跳号）；越界 → `40000`。
- **CreateProduct（改造）**：`code` 非空 → 走 `012` 原逻辑（`ExistsByCodeAsync` → `40101`）；`code` 空 → `ICodeGenerator.GenerateAsync(Product, today)`（`40181`）→ `ExistsByCodeAsync` 冲突则重试 1 次 → 仍冲突 `40182` → 组 `Product` + `Inventory` 落库（`012` 既有事务）。

### 3.5 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `SaveCodeRuleRequest` | `prefix` ≤ 10 且匹配 `PrefixPattern`；`datePattern` 枚举合法；`serialLength` 3–8 |
| `GetCodeRulesRequest` | 无参 |
| `CreateProductRequest`（改造） | `code` **改为可空**（≤ 32，非空时 `CodePattern`）；其余不变（`012` §3.5） |

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── codeRule.ts                     # 编码规则接口与类型
└── views/
    ├── CodeRuleManagement/
    │   └── CodeRulesView.vue           # 规则配置页
    └── ProductManagement/
        └── ProductFormDrawer.vue       # 改造：编码可留空 + 提示（既有文件）
```

### 4.2 路由与菜单

| path | name | 组件 |
|---|---|---|
| `code-rules` | `codeRules` | `CodeRulesView` |

- 「系统」分组续行「编码规则」；`meta.permission = 'codeRules.view'`。

### 4.3 页面交互

- **`CodeRulesView.vue`**：资源类型行（首期「商品」）+ 前缀 / 日期段（下拉）/ 序号位数（`a-input-number`）+ 实时**预览**（用当天生成的样例编码）；保存按钮 loading；规则未配置时预览为空。
- **`ProductFormDrawer.vue`（改造）**：新增态编码输入框在**规则已配置时可留空**（占位提示「留空按编码规则自动生成」）；未配置时必填（与升级前一致）。

### 4.4 接口层

- `api/codeRule.ts`：`getCodeRules` / `saveCodeRule`；`CODE_DATE_PATTERN_META`（值 → 文案）。
- `api/product.ts`：`CreateProductPayload.code` 改为可选。

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 |
|---|---|---|
| 序号状态存**规则行**而非独立计数表 | `CodeRules.LastKey` / `LastSerial` | 一处状态、一处锁，避免两表一致性问题 |
| 重置维度含**前缀** | `(资源, 前缀, 日期段值)` | 改前缀即启用新序列，符合运营直觉 |
| 未配置时**保持必填** | `40181` | 不破坏 `012` 既有用法；渐进启用 |
| 配置变更**不重置序号** | `SaveCodeRule` 不动 `LastKey` / `LastSerial` | 避免"改个位数就跳号"的运营困扰 |
| 并发用**行锁**而非乐观重试 | `FOR UPDATE` | 生成必须唯一，行锁最直接；应用层重试仅兜底撞码 |
| 只做**商品** | 资源类型枚举续行 | 往来需先加 `Code` 列（改造面大），另评；机制已通用 |
| 三段固定结构 | 前缀 + 日期段 + 序号 | 覆盖绝大多数主数据编码诉求；自由模板（正则 / 多段）收益低 |
| 无新增错误码体系 | 只用 `40181` / `40182` | 生成的失败面窄，两个码足够 |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

- **生成格式**：各 `DatePattern` × 各 `SerialLength`；前缀空 / 非空；补零。
- **重置**：同日期段连续自增；跨日期段（换日 / 换月）重置为 1。
- **并发**：同规则并发生成 → 序号不重复（或经行锁串行）。
- **商品接入**：`code` 空 + 规则存在 → 生成并落库；`code` 空 + 无规则 → `40181`；`code` 空 + 生成撞码 → 重试 / `40182`；`code` 非空 → 走 `012` 原路径（回归）。
- **规则保存**：前缀越界 / 非法字符 `40000`；位数越界 `40000`；保存不重置序号。
- **字段约束一致性**：常量与 EF 列长一致；`CreateProductRequest.code` 可空后边界回归。
