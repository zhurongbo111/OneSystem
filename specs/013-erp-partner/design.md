---
created: 2026-09-13
updated: 2026-09-20
---

# 设计规格：往来单位（erp-partner）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `user-management` 为结构参照；字段约束单一来源（后端规则 §5.3）同样适用。
> 本规格为进销存功能组往来单位域，开单下拉数据源的消费方为 erp-purchase / erp-sale。

## 1. 总体设计

```
往来单位（前端 /partners）
  → PartnersController
    → App.Core/Features/Partners/<Action>/*RequestHandler
      → IPartnerRepository（App.Core）→ PartnerRepository（App.Infrastructure，EF Core）
        → AppDbContext → PostgreSQL
```

核心原则：

- **供应商 / 客户合并一张表** `Partners` + `PartnerType`（1=供应商 2=客户 3=两者），一份档案可同时用于采购与销售。
- **名称唯一且不可改**：数据库唯一索引大小写敏感兜底，应用层 `ExistsByNameAsync` 大小写不敏感（`ToLower` 比较，与用户模块一致）。
- **只停用不删除**：停用单位不出现在开单下拉（由 erp-purchase / erp-sale 过滤），已有单据保留引用。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset`（实体 / DTO / 仓储签名 / 请求入参），Npgsql 映射 `timestamptz`（后端规则 §5.2）。
> 状态枚举统一 `Enabled = 1 / Disabled = 0` 小整数，PG `smallint`。

### 2.1 实体 `App.Core/Entities/Partner.cs` 与表 `Partners`（供应商 / 客户合并）

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `Name` | `string` | `varchar(50)` | NOT NULL，唯一索引 | 单位名称，创建后不可改 |
| `Type` | `PartnerType` | `smallint` | NOT NULL | 1=供应商 2=客户 3=两者 |
| `Contact` | `string?` | `varchar(20)` | NULL | 联系人 |
| `Phone` | `string?` | `varchar(20)` | NULL | 联系电话 |
| `Address` | `string?` | `varchar(100)` | NULL | 地址 |
| `Remark` | `string?` | `varchar(200)` | NULL | 备注 |
| `Status` | `PartnerStatus` | `smallint` | NOT NULL，默认 `1` | 启用 / 停用 |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | |
| `CreatedBy` / `UpdatedBy` | `Guid?` | `uuid` | NULL | |

- 枚举：`App.Core/Entities/PartnerType.cs`（`PartnerType { Supplier = 1, Customer = 2, Both = 3 }`）、`PartnerStatus.cs`（`PartnerStatus { Disabled = 0, Enabled = 1 }`）。
- 采购单校验供应商：`Type in (Supplier, Both)` 且启用；销售单校验客户：`Type in (Customer, Both)` 且启用（在 erp-purchase / erp-sale 的 Handler 中执行）。
- 唯一性：数据库唯一索引大小写敏感；应用层 `ExistsByNameAsync` 大小写不敏感（`ToLower` 比较），与用户模块一致。
- 实体配置 `App.Infrastructure/Persistence/Configurations/PartnerConfiguration.cs`。

### 2.2 EF Core 与迁移

- `AppDbContext` 新增 1 个 `DbSet`：`Partners`；实体配置 `PartnerConfiguration.cs`。
- 迁移：`dotnet ef migrations add AddErpPartner -p src/App.Infrastructure -s src/App.Api`（**增量迁移**，不动既有迁移）。
- 无种子数据（业务数据全部经界面录入）。

### 2.3 字段约束单一来源（`App.Core/Entities/PartnerFieldConstraints.cs`）

| 常量 | 值 |
|---|---|
| `NameMinLength` / `NameMaxLength` | 1 / 50 |
| `ContactMaxLength` | 20 |
| `PhoneMaxLength` / `PhonePattern` | 20 / `^1[3-9]\d{9}$`（与用户手机号同语义，各自常量类定义） |
| `AddressMaxLength` | 100 |
| `RemarkMaxLength` | 200 |
| `KeywordMaxLength` | 50（查询关键词，对齐 Name 列长） |

- EF 实体配置（`HasMaxLength` / `IsRequired` / `HasColumnType` / 索引）与全部 `RequestValidator` 均引用上述常量，禁止硬编码。
- 一致性由单测守护（扩展 `FieldValidationConsistencyTests`）：EF 模型实际 `HasMaxLength` == 常量；同一字段跨用例 Validator「边界值通过 / 越界拒绝」一致；查询关键词长度不超对应列长。

## 3. 后端设计

### 3.1 仓储接口（新增，`App.Core/Abstractions/`）

`IPartnerRepository`：

| 方法 | 说明 |
|---|---|
| `Task<Partner?> GetByIdAsync(Guid id, ...)` | 按 id（含跟踪，供编辑 / 启停） |
| `Task<bool> ExistsByNameAsync(string name, Guid? excludeId, ...)` | 名称存在性（大小写不敏感） |
| `Task<(IReadOnlyList<Partner> Items, int Total)> GetPagedAsync(string? keyword, PartnerType? type, PartnerStatus? status, int page, int pageSize, ...)` | 分页 + 筛选（keyword 匹配名称 / 联系人） |
| `Task AddAsync(Partner, ...)` / `UpdateAsync(Partner, ...)` | 新增 / 更新 |

- 单一仓储写由仓储自身 `SaveChangesAsync` 保证，无需 `IUnitOfWork`。
- 审计字段统一由 Handler 经 `ICurrentUser` 获取后随实体传入，仓储不感知当前用户。

### 3.2 错误码（追加到 `App.Core/Errors/ErrorCode.cs`）

| code | 常量 | 含义 |
|---:|---|---|
| 40102 | `PartnerNameExists` | 往来单位名称已存在 |
| 40119 | `PartnerTypeNarrowingNotAllowed` | 往来单位类型不允许收窄（只可保持原类型或改为两者） |

> `40400 NotFound` / `40000 Validation` 复用全局。其余进销存错误码由 erp-product / erp-purchase / erp-sale 各自定义。

### 3.3 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

| 接口 | 方法 | 用例目录 | `data` 响应 | 错误码 |
|---|---|---|---|---|
| `/api/partners` | GET | `Partners/GetPartners` | `PagedResult<PartnerListItemDto>` | 40000 |
| `/api/partners` | POST | `Partners/CreatePartner` | `PartnerDetailDto` | 40000 / 40102 |
| `/api/partners/{id:guid}` | GET | `Partners/GetPartnerById` | `PartnerDetailDto` | 40400 |
| `/api/partners/{id:guid}` | PUT | `Partners/UpdatePartner` | `PartnerDetailDto` | 40000 / 40400 |
| `/api/partners/{id:guid}/status` | PUT | `Partners/UpdatePartnerStatus` | `PartnerDetailDto` | 40000 / 40400 |

### 3.4 关键用例流程（Handler）

**CreatePartner**：`ExistsByNameAsync(name, null)` 为真 → `40102`；否则组 `Partner`（`Status=Enabled`、审计字段取 `ICurrentUser`）→ 新增。

**UpdatePartner**：`GetByIdAsync`（不存在 → `40400`）→ 更新类型 / 联系人 / 电话 / 地址 / 备注（**不触碰 `Name`**）→ 更新审计。
- 可选字段（联系人 / 电话 / 地址 / 备注）遵循 `AGENTS.md` §4.5 全量覆盖语义（缺字段 / 空串 / 纯空白一律清空落 `null`）；`Name` 为不可改字段（接口不接受）。
- **类型只放宽不收窄**（跨字段业务约束，需读原档案，归 Handler 判定）：仅允许 `Type == 原类型` 或 `Type == Both`；供应商 / 客户互改、`Both` 改回单一类型抛 `40119`。理由：采购 / 销售单据（含编辑）按档案当前类型重校验（如 `UpdatePurchaseOrderRequestHandler`），收窄会使既有单据不可编辑。

**UpdatePartnerStatus**：`GetByIdAsync`（不存在 → `40400`）→ 置 `Status` → 更新审计。

**GetPartners**：仓储分页筛选（keyword 匹配名称 / 联系人，type / status 可空）→ Handler 映射 DTO。

### 3.5 校验规则（FluentValidation，仅格式层，引用 §2.3 常量）

| 请求 | 规则 |
|---|---|
| `CreatePartnerRequest` | `name` 必填 1–50；`type` 必填且 ∈ {1,2,3}；`contact` ≤20；`phone` 选填 `PhonePattern`；`address` ≤100；`remark` ≤200 |
| `UpdatePartnerRequest` | 同 create，去掉 `name`（不可改）；类型「只放宽不收窄」属跨字段业务约束，归 Handler（见 §3.4） |
| `GetPartnersRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 50（`KeywordMaxLength`）；`type` / `status` 可空或合法值 |
| `UpdatePartnerStatusRequest` | `status` ∈ {0, 1} |

- 存在性 / 唯一性等业务约束一律在 Handler 判断（后端规则 §4.1）。

### 3.6 Swagger

- **不分组**（用户已确认）：维持现有单文档 Swagger，本规格新增接口按现有方式正常出现在文档中，不使用 `ApiExplorerSettings.Group`。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── partner.ts            # 往来单位接口层
└── views/
    └── PartnerManagement/
        ├── PartnersView.vue      # 往来单位列表页
        └── PartnerFormDrawer.vue # 新增/编辑抽屉
```

- 往来单位字段 ≤ 8，新增 / 编辑用**抽屉**（`a-drawer`，`unmount-on-close`，底部自定义操作栏）。
- 详情：按 §5.5 详情页独立页面约定，但往来单位字段少（< 5 个可复用表单 disabled 态例外条款）：**往来单位不做独立详情页**，查看用抽屉 disabled 态（design 记录该决策）。

### 4.2 接口层

- `src/api/partner.ts`：TS 类型与后端 DTO（camelCase）一一对应；函数经 `src/api/request.ts` 统一封装（解包 `data`、40100 处理）。
- 开单下拉数据源：erp-purchase / erp-sale 页面复用本模块导出的 `getPartners`（传 `status=1` + `type` 筛选），本规格仅交付接口层与类型。

### 4.3 路由与菜单

`src/router/index.ts` 新增（均 `meta.requiresAuth: true`）：

| path | name | 组件 |
|---|---|---|
| `partners` | `partners` | `PartnersView` |

`AppLayout.vue` 侧边菜单「进销存」分组追加子项「往来单位」`partners`（分组由 erp-product 创建；若 erp-partner 先交付则本规格同时创建分组，key `erp`、图标 `IconStorage`、默认展开）。

### 4.4 页面交互

**往来单位列表 `PartnersView.vue`**（参照 `UsersView.vue`）：
- 筛选行：关键词（名称 / 联系人）+ 类型下拉（全部 / 供应商 / 客户 / 两者）+ 状态 + 搜索 / 重置。
- 表格列：序号、名称、类型（`a-tag`：供应商蓝 / 客户绿 / 两者紫）、联系人、电话、地址、状态、操作列（编辑 / 停用或启用（popconfirm）/ 详情）；服务端分页。
- 详情用 `PartnerFormDrawer` disabled 态查看。

**往来单位抽屉 `PartnerFormDrawer.vue`**：
- 新增：名称、类型、联系人、电话、地址、备注；编辑：同上去掉名称（只读展示）。
- 编辑态类型单选按原类型**只放宽不收窄**渲染：保持原类型与「两者」可选，会收窄的单一类型项 `disabled`（供应商 / 客户→仅可保持或改「两者」，「两者」→仅可保持），并给出说明文案；新增态三项均可选（与后端 §3.4 规则一致，前端做引导、后端做兜底）。
- 打开时先 `Object.assign(form, emptyForm())` 重置（防数据串台）；提交 `submitting` + 防重入。

### 4.5 按钮 loading（遵循前端规则 §4.6）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 列表查询 | `loading` | 搜索 / 翻页 + 表格 |
| 抽屉提交 | `submitting` | 提交按钮 |
| 往来单位启停（行内） | `togglingId` | `:loading="togglingId === row.id"` |

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| 供应商 / 客户合并一张表 + `Type` | `Partners` + `PartnerType` | 一份档案可既是客户又是供应商；类型校验在 erp-purchase / erp-sale 的 Handler（`Both` 兼容两类单据） |
| 名称唯一且不可改 | 应用层大小写不敏感 + 数据库唯一索引兜底 | 避免单据引用歧义，同用户名不可改原则；名称是业务标识 |
| 类型只放宽不收窄 | 仅允许保持原类型或改为 `Both`，反例抛 `40119` | 档案被历史单据引用，收窄会使既有单据在编辑时类型重校验失败；放宽（→`Both`）不影响任何既有单据 |
| 只停用不删除 | 停用不可被新单据选择，保留历史引用 | 单据引用往来单位，删除会破坏审计轨迹 |
| 往来单位不做独立详情页 | 抽屉 disabled 态查看 | 字段 < 5 个的简单实体，符合前端规则 §5.5 例外条款 |
| 无 RBAC | 登录即可见往来单位菜单 | 用户确认本期不做权限；后续权限模块统一接入 |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储接口；`TestCurrentUser`（`ICurrentUser`）同 `user-management` 测试约定；时间用固定 `DateTimeOffset` 入参或注入时钟，不读 `DateTime.Now`（Handler 时间来源统一为仓储传入的 `utcNow` 参数，同用户模块）。

- **GetPartners**：关键词（命中名称或联系人）/ 类型 / 状态筛选传参断言；分页映射。
- **CreatePartner**：成功；重名（大小写不敏感）→ `40102`。
- **UpdatePartner**：成功（断言不改 `Name`）；不存在 → `40400`；**类型收窄**（供应商→客户、两者→供应商 / 客户）→ `40119`；**类型放宽**（供应商 / 客户→`Both`）与保持原类型成功。
- **UpdatePartnerStatus**：成功；不存在 → `40400`。
- **GetPartnerById**：存在 / 不存在（`40400`）。
- **字段约束一致性**（扩展 `FieldValidationConsistencyTests`）：Name 50 通过 / 51 拒绝；Phone 边界（11 位合法通过 / 10 位拒绝）；EF `HasMaxLength` == `PartnerFieldConstraints`；`keyword` 50 通过 / 51 拒绝。
