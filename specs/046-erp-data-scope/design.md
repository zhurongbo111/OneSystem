---
created: 2026-10-07
updated: 2026-10-07
---

# 设计规格：数据级权限 / 数据范围（erp-data-scope）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织；本规格为**横向改造**（角色加属性 + 受约束资源的查询接口追加过滤），以 `028-erp-rbac` 为结构参照。
> 权限机制复用 `028`；组织 / 员工复用 `030`；越权统一返回既有 `40300`（`028` 生产码），**不新增业务错误码**。

## 0. 约定正文（唯一事实源）

### 0.1 数据范围取值与口径

| 枚举 | 取值 | 文案 | 可见范围 |
|---|---|---|---|
| `DataScope` | `All = 0` | 全部 | 不过滤 |
| | `DepartmentAndBelow = 1` | 本部门及下级 | 本部门 + 全部下级部门的员工所属账号 |
| | `Department = 2` | 本部门 | 本部门员工的所属账号 |
| | `Self = 3` | 仅本人 | 仅自己 |

- **多角色取最宽（并集）**：由宽到窄 `All > DepartmentAndBelow > Department > Self`；任一角色为 `All` 即 `All`。
- **`SuperAdmin` 恒为 `All`**（不逐点存储，与 `028` §0.3 一致）。
- **无绑定员工**（当前用户 `Employees.UserId` 无记录）时：`Department*` **退化为 `Self`**；`All` / `Self` 不受影响。
- **默认值**：新建角色默认 `Self`；**迁移回填所有既有角色为 `All`**（保证升级无感，§5）。

### 0.2 受约束资源（唯一事实源）

「数据范围」只作用于下表资源；判据 = **实体带 `CreatedBy`（创建人）**且属业务单据 / 留痕。

| 资源 | 后端读模型入口 | 归属字段 | 列表 / 详情 / 导出 |
|---|---|---|---|
| 采购入库 `purchases` | `IPurchaseReceiptRepository` | `CreatedBy` | 全部约束 |
| 销售出库 `sales` | `ISalesShipmentRepository` | `CreatedBy` | 全部约束 |
| 采购退货 `purchaseReturns` | `IPurchaseReturnRepository` | `CreatedBy` | 全部约束 |
| 销售退货 `salesReturns` | `ISalesReturnRepository` | `CreatedBy` | 全部约束 |
| 收付款 `settlements` | `ISettlementRepository` | `CreatedBy` | 全部约束 |
| 报价单 `quotations` | `IQuotationRepository` | `CreatedBy` | 全部约束 |
| 发票 `invoices` | `IInvoiceRepository` | `CreatedBy` | 全部约束 |
| 记账凭证 `vouchers` | `IVoucherRepository` | `CreatedBy` | 全部约束 |
| 操作日志 `auditLogs`（`029`） | `IAuditLogRepository` | `OperatorId` | 全部约束 |

- **不约束**（本期）：主数据与档案（商品 / 往来 / 仓库 / 员工 / 科目…）、库存台账与流水、报表（`025` / `033`）、审批（`042`）、CRM（`043` / `045`）、站内消息（`041`）。理由：主数据与台账是全组织共享口径，按部门裁剪会产生"对账对不平"；报表与 CRM 的可见性另有诉求，届时另立。
- **过滤位置**：统一在**仓储查询方法**内追加；Handler 只负责把 `DataScopeFilter` 传入（后端规则 §4.1：查库约束在 Handler，但可见性属横切，由 Provider 解析后下传）。

### 0.3 越权详细判据

- **列表 / 导出**：静默过滤（不报错），仅返回范围内数据。
- **详情 / 单据动作（作废 / 编辑 / 审批等按 id 的操作）**：目标实体 `CreatedBy ∉ visibleUserIds` 且 `!All` → **`40300`（无权限）**，与 `028` 语义一致（不是 `40400`）。
- 判据实现：受约束资源的 `GetByIdAsync` 由 Handler 在取到实体后调用 `IDataScopeProvider.CanAccess(entity.CreatedBy)` 统一校验。

### 0.4 权限点与菜单

- **无新增权限点**：数据范围在角色表单内配置，受既有 `roles.view` / `roles.update` 保护（`028` §0.2）。
- **无新增菜单**：不引入新页面（仅在角色表单增字段）。

## 1. 总体设计

```
角色表单（前端 /roles 抽屉）
  → RolesController.UpdateRole（请求体增 dataScope）
    → Role.DataScope 落库

数据范围强制（横切，受约束资源）
  HTTP 请求 → 认证（JWT）→ PermissionAuthorizationFilter（028，功能级）
                    ↓ 通过
    IDataScopeProvider.ResolveAsync(userId)（单请求缓存）
      → 查 UserRoles → Roles.DataScope → 取最宽
      → 经 Employees(UserId→DepartmentId) + 部门子树 → 展开 visibleUserIds
    → DataScopeFilter { All, VisibleUserIds }
    → 受约束仓储查询 / Handler 详情校验
      → 列表：WHERE CreatedBy IN visibleUserIds
      → 详情：CreatedBy ∈ visibleUserIds ? 放行 : 40300
```

核心原则：

- **功能级先于数据级**：`028` 过滤器仍是唯一功能级入口；本规格是**第二道**（数据范围），Handler 不写功能级判断。
- **解析一次、请求内复用**：`IDataScopeProvider` Scoped + 单请求缓存（与 `IPermissionResolver` 同形）。
- **默认放开（迁移后）**：既有角色回填 `All`，确保升级不改变任何可见性。
- **单一判据**：可见性判据（§0.3）由 `IDataScopeProvider.CanAccess` 单点实现，仓储与 Handler 复用。

## 2. 数据模型

### 2.1 实体 `Role` 扩展（表 `Roles`）

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `DataScope` | `DataScope` | `smallint` | NOT NULL，默认 `All` | 新增列 |

- 迁移：`dotnet ef migrations add AddErpDataScope -p src/App.Infrastructure -s src/App.Api`；**回填既有行 `All`**（`UPDATE "Roles" SET "DataScope" = 0`）。
- 种子：`DatabaseInitializer` 中内置角色 `SuperAdmin` / `Staff` 幂等置 `All`。
- 无新实体、无新表。

### 2.2 枚举（`App.Core/Entities/`）

| 枚举 | 取值 |
|---|---|
| `DataScope` | `All = 0` / `DepartmentAndBelow = 1` / `Department = 2` / `Self = 3` |

## 3. 后端设计

### 3.1 解析组件（`App.Core/Abstractions/` + `App.Infrastructure/`）

| 类型 | 位置 | 说明 |
|---|---|---|
| `DataScopeFilter` | `App.Core/Abstractions/`（`sealed record`） | `{ bool All; IReadOnlySet<Guid> VisibleUserIds }`；`All == true` 时忽略集合 |
| `IDataScopeProvider` | `App.Core/Abstractions/` | `Task<DataScopeFilter> GetAsync(CancellationToken)`（取当前用户）；`bool CanAccess(Guid createdBy)`（基于缓存） |
| `DataScopeProvider` | `App.Infrastructure/Auth/` | 实现：`ICurrentUser.UserId()` → `IUserRoleRepository` 取角色 → 取最宽 `DataScope` → 经 `IEmployeeRepository`（`UserId → DepartmentId`）+ `IDepartmentRepository`（子树）展开 `VisibleUserIds`；`All` 短路；**Scoped + 单请求缓存** |

- 仓储扩展：`IUserRoleRepository` 追加 `GetRoleIdsByUserAsync`（若已有则复用）；`IEmployeeRepository` 追加 `GetUserIdsByDepartmentIdsAsync(departmentIds)`（**批量取数，避免逐部门 N+1**，CONTEXT.md §2「导出用批量查询」惯例）。
- 部门子树：`IDepartmentRepository` 追加 `GetDescendantIdsAsync(rootId)`（复用 `030` 的父链 / 子树遍历）。

### 3.2 仓储过滤接入（§0.2 九类资源）

| 仓储方法 | 变更 |
|---|---|
| 各资源 `GetPagedAsync` | 增可空 `IReadOnlyCollection<Guid>? visibleUserIds`（`null` = 不过滤）；SQL 追加 `CreatedBy IN (...)` / 操作日志用 `OperatorId` |
| 各资源 `GetByIdAsync` | 不变（详情由 Handler 用 `CanAccess` 校验，避免仓储感知可见性） |
| 导出用例 | 复用同 `GetPagedAsync`（导出走同一过滤参数），无需另写 |

- 参数为 `null` 语义 = `All`；非空 = 白名单集合（`Self` 时集合为 `{自己}`）。

### 3.3 用例改造

| 接口 | 变更 |
|---|---|
| `PUT /api/roles/{id:guid}` | `UpdateRoleRequest` 增 `dataScope`（必填枚举）；`Role.DataScope` 更新；`SuperAdmin` 强制 `All`（`40175` 既有） |
| `POST /api/roles` | `CreateRoleRequest` 增 `dataScope`（可选，默认 `Self`） |
| `GET /api/roles` / `GET /api/roles/{id:guid}` | 出参增 `dataScope`（含文案，`RoleListItemDto` / `RoleDetailDto`） |
| §0.2 九类资源的 `Get*`（列表）与 `Export*` | Handler 注入 `IDataScopeProvider` → 取 `DataScopeFilter` → 传 `visibleUserIds` 给仓储 |
| §0.2 九类资源的 `Get*ById` | Handler 取实体后 `CanAccess(CreatedBy)` → 不通过 `40300` |

### 3.4 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `CreateRoleRequest` / `UpdateRoleRequest` | 增 `dataScope` 枚举合法性（`UpdateRoleRequest` 必填；`CreateRoleRequest` 缺省取 `Self`） |

### 3.5 错误码

- **不新增业务码**；越权一律 `40300`（`028` 既有生产码）。
- `ROADMAP` §6「下一个可用」**保持 `40176`**（本规格不占用）。

## 4. 前端设计

### 4.1 目录（复用 `028` 的 `RoleManagement/`）

```
src/
├── api/
│   └── role.ts                       # 类型扩充：dataScope（+ 文案映射 DATA_SCOPE_META）
└── views/
    └── RoleManagement/
        └── RolesView.vue             # 抽屉表单增「数据范围」a-select
```

### 4.2 页面交互

- **`RolesView.vue`**：角色抽屉表单在「权限树」之上增「数据范围」`a-select`（四选项，文案见 §0.1）；列表增「数据范围」列（`a-tag`）。
- `SuperAdmin` 行数据范围**置灰不可改**（后端 `40175` 兜底）。
- 其余列表 / 详情页**无改动**（过滤在后端）。

### 4.3 接口层

- `api/role.ts`：`RoleListItem` / `RoleDetail` 增 `dataScope: number`；`DATA_SCOPE_META`（值 → 中文文案与 `a-tag` 颜色）集中本文件（前端规则 §3）。

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 |
|---|---|---|
| 数据范围挂在**角色**而非用户 | `Role.DataScope` | 与 `028`「权限由角色配置」一致，避免用户级散落配置 |
| 多角色取**并集（最宽）** | 宽窄序取最大 | 与 `028` 权限并集语义一致；交集会导致"多担一职反而看不到" |
| 过滤**在仓储**、判据在 Provider | §0.3 | 列表需 SQL 侧过滤（分页正确性）；详情单点校验避免仓储感知可见性 |
| 归属用 `CreatedBy`（创建人） | 非"经办人" | 现有单据无独立经办人字段，创建人即录入人；引入经办人属另一模型，本期不做 |
| 越权详情返回 `40300` 而非 `40400` | 与 `028` 一致 | 避免"用 404 探测存在性"与"无权限"语义混淆（`AGENTS.md` §4.2） |
| 迁移回填 `All` | 默认放开 | 保证既有角色升级无感；新角色默认 `Self` 收紧 |
| 主数据 / 台账 / 报表**不约束** | §0.2 | 共享口径按部门裁剪会产生对账不一致；另立评估 |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

- **解析**：单角色各 `DataScope` → 正确 `VisibleUserIds`；多角色并集取最宽；`SuperAdmin` → `All`；无绑定员工 → `Dept*` 退化 `Self`。
- **过滤**：九类资源列表 `visibleUserIds` 生效（含 `null` 不过滤）；导出同口径。
- **越权**：目标 `CreatedBy` 不在范围内 → 详情 / 按 id 动作 `40300`；在范围内放行。
- **角色域**：`dataScope` 更新持久化；`SuperAdmin` 强制 `All`（`40175`）。
- **迁移**：回填后既有角色 `DataScope == All`（行为回归）。
