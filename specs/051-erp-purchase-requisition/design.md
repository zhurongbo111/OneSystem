---
created: 2026-10-07
updated: 2026-10-07
---

# 设计规格：采购请购单（erp-purchase-requisition）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织；以 `037-erp-quotation`（三态 + 一次性转单）为结构参照，订单复用 `024` 的 `PurchaseOrders` 表与仓储。
> 采购订单结构 / 状态见 `specs/024-erp-order-flow/design.md` §0 / §2.2；单号前缀全域表见 `specs/ROADMAP.md` §6.7。

## 0. 约定正文（唯一事实源）

### 0.1 状态与流转

| 枚举 | 取值 | 文案 | `a-tag` |
|---|---|---|---|
| `RequisitionStatus` | `Draft = 0` / `Converted = 1` / `Voided = 2` | 草稿 / 已转订单 / 已作废 | `blue` / `green` / `gray` |

**允许的操作**（其余 → `40183`）：

| 当前状态 | 可编辑 | 可作废 | 可转订单 |
|---|---|---|---|
| `Draft` | 是 | 是 | 是 |
| `Converted` | 否 | 否 | 否（`40184`） |
| `Voided` | 否 | 否 | 否（`40184`） |

### 0.2 唯一性与单号

| 对象 | 字段 | 规则 | 冲突错误码 |
|---|---|---|---|
| 采购请购单 | `RequisitionNo` | 唯一；`RQ + yyyyMMdd + 4 位序号`（前缀全域分配见 `ROADMAP` §6.7，`PR` 已被采购退货占用故取 `RQ`） | — |

### 0.3 转采购订单语义

- **一次性**：同一请购单只能转一次；成功后置 `Converted` 并记 `ConvertedOrderId` / `ConvertedOrderNo`。
- **同事务**：建 `PO`（`024` 的 `PurchaseOrders` + 明细，`FlowStatus = Pending`、`OrderDate` = 转单当天）+ 回写请购单，事务内完成。
- **明细复制**：商品 / 数量 / 单价（预估）/ 小计按请购单原样复制；供应商在订单上选择（请购单不指定）。
- **不动库存 / 流水 / 资金**（请购与订单均为计划单，同 `024` §1 原则）。

### 0.4 权限点（在 `028` §0.2 表续行）

| 域 | key 前缀 | 权限点（动作） | 对应接口 / 页面 |
|---|---|---|---|
| 采购请购 | `purchaseRequisitions` | `view` / `create` / `update` / `void` / `convert` | `/api/purchase-requisitions*`、`/purchase-requisitions` |

### 0.5 菜单归属（在 `025` §0.2 表续行）

「采购（`purchase`）」分组**首位**续行子项：

| 顶级分组 | 子项（key） | 引入规格 |
|---|---|---|
| 采购（`purchase`） | 采购请购（`purchaseRequisitions`） | `051` |

## 1. 总体设计

```
采购请购（前端 /purchase-requisitions，域目录 PurchaseRequisitionManagement/）
  → PurchaseRequisitionsController
    → App.Core/Features/PurchaseRequisitions/<Action>/*RequestHandler
      → IPurchaseRequisitionRepository（主表 + 明细）
        + IPurchaseOrderRepository（转单建单，024）
        + IEmployeeRepository（申请人存在性，030）
        → PostgreSQL（PurchaseRequisitions / PurchaseRequisitionItems / PurchaseOrders / PurchaseOrderItems）  ← 不动库存 / 流水
```

核心原则：

- **请购是需求单**：不触碰库存 / 流水 / 资金，与报价单同性质。
- **转单同事务**：建订单与回写请购单在同一 `IUnitOfWork` 内，避免"订单建了但请购单未锁定"。
- **状态单字段**：`RequisitionStatus` 覆盖三态，不引入第二个状态列。

## 2. 数据模型

### 2.1 实体 `App.Core/Entities/PurchaseRequisition.cs` 与表 `PurchaseRequisitions`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `RequisitionNo` | `string` | `varchar(20)` | NOT NULL，唯一索引 | `RQ + yyyyMMdd + 4` |
| `RequesterId` | `Guid?` | `uuid` | NULL，FK → `Employees(Id)`，索引 | 申请人（员工，可选） |
| `RequesterName` | `string?` | `varchar(50)` | NULL | 申请人姓名**快照** |
| `RequisitionDate` | `DateTimeOffset` | `timestamptz` | NOT NULL | 需求日期 |
| `ExpectedDate` | `DateTimeOffset?` | `timestamptz` | NULL | 期望到货日期 |
| `TotalAmount` | `decimal` | `numeric(18,2)` | NOT NULL | Σ 小计（预估，后端计算） |
| `Status` | `RequisitionStatus` | `smallint` | NOT NULL，默认 `Draft` | 见 §0.1 |
| `ConvertedOrderId` | `Guid?` | `uuid` | NULL | 转出的采购订单 |
| `ConvertedOrderNo` | `string?` | `varchar(20)` | NULL | 转出订单号快照 |
| `Remark` | `string?` | `varchar(200)` | NULL | |
| 审计 | | | | `CreatedAt` / `UpdatedAt` / `CreatedBy` / `UpdatedBy` |

明细（`PurchaseRequisitionItem` / `PurchaseRequisitionItems`）：

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `RequisitionId` | `Guid` | `uuid` | NOT NULL，FK，索引 | |
| `ProductId` | `Guid` | `uuid` | NOT NULL，FK → `Products(Id)` | |
| `ProductName` / `Unit` | `string` | `varchar(50)` / `varchar(10)` | NOT NULL | 快照 |
| `Quantity` | `int` | `integer` | NOT NULL，≥ 1 | 需求数量 |
| `UnitPrice` | `decimal` | `numeric(18,2)` | NOT NULL，≥ 0 | 预估单价 |
| `Subtotal` | `decimal` | `numeric(18,2)` | NOT NULL | 后端计算 |

### 2.2 字段约束（单一来源）

- **不新建常量类**：列长与约束复用 `024` §2.5（`OrderFieldConstraints` 等）与 `037` 惯例（快照列复用既有常量）。

### 2.3 迁移与种子

- 迁移：`dotnet ef migrations add AddErpPurchaseRequisition -p src/App.Infrastructure -s src/App.Api`（建 2 表 + 索引；FK 不级联删除）。
- 无种子数据。

## 3. 后端设计

### 3.1 仓储接口（`App.Core/Abstractions/`）

| 接口 / 方法 | 说明 |
|---|---|
| `IPurchaseRequisitionRepository.GetPagedAsync(...)` | 列表（关键词 / 状态 / 申请人 / 日期区间，明细行数聚合） |
| `IPurchaseRequisitionRepository.GetByIdAsync` / `AddAsync` / `UpdateAsync`（明细全量替换） | |
| `IPurchaseRequisitionRepository.GenerateNoAsync(date, ...)` | 单号 `RQ` |
| `IPurchaseRequisitionRepository.MarkConvertedAsync(id, orderId, orderNo, ...)` | 回写锁定 |

- 读模型：`PurchaseRequisitionListItem`（主表 + 明细行数）、`PurchaseRequisitionDetail`（主表 + 明细）。
- 转单复用 `IPurchaseOrderRepository.AddAsync`（`024`）。

### 3.2 错误码（`ErrorCode.cs`，从 `40183` 起）

| code | 常量 | 含义 |
|---:|---|---|
| 40183 | `RequisitionNotEditable` | 请购单非草稿，不可编辑 / 作废 |
| 40184 | `RequisitionCannotConvert` | 请购单不可转单（非草稿 / 已转过） |

> 下一个可用业务码 → `40185`（`ROADMAP` §6 顶部同步）。

### 3.3 用例与接口

| 接口 | 方法 | 用例目录 | `data` | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/purchase-requisitions` | GET | `PurchaseRequisitions/GetPurchaseRequisitions` | `PagedResult<..ListItemDto>` | `purchaseRequisitions.view` / 40000 |
| `/api/purchase-requisitions` | POST | `PurchaseRequisitions/CreatePurchaseRequisition` | `..DetailDto` | `purchaseRequisitions.create` / 40000 / 40400 |
| `/api/purchase-requisitions/{id:guid}` | GET | `PurchaseRequisitions/GetPurchaseRequisitionById` | `..DetailDto` | `purchaseRequisitions.view` / 40400 |
| `/api/purchase-requisitions/{id:guid}` | PUT | `PurchaseRequisitions/UpdatePurchaseRequisition` | `..DetailDto` | `purchaseRequisitions.update` / 40000 / 40183 / 40400 |
| `/api/purchase-requisitions/{id:guid}/void` | PUT | `PurchaseRequisitions/VoidPurchaseRequisition` | `..DetailDto` | `purchaseRequisitions.void` / 40183 / 40400 |
| `/api/purchase-requisitions/{id:guid}/convert` | POST | `PurchaseRequisitions/ConvertPurchaseRequisition` | `{ requisition, orderId, orderNo }` | `purchaseRequisitions.convert` / 40184 / 40400 |

### 3.4 关键用例流程（Handler）

- **CreatePurchaseRequisition**：申请人可选（存在性 → `40400`）；商品存在性；`Subtotal` / `TotalAmount` 后端计算；生成单号；落库（`Draft`）。
- **UpdatePurchaseRequisition**：`Status != Draft` → `40183`；明细全量替换；重算金额。
- **VoidPurchaseRequisition**：`Status != Draft` → `40183`；置 `Voided`。
- **ConvertPurchaseRequisition**：`Status != Draft` → `40184`；组装 `PurchaseOrder`（`FlowStatus = Pending`、`OrderDate` = 转单当天、明细复制）→ 同事务 `AddAsync` + `MarkConvertedAsync` → 返回订单号（`037` 范式）。

### 3.5 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `Create/UpdatePurchaseRequisitionRequest` | `requisitionDate` 必填；`expectedDate` 可空且 ≥ `requisitionDate`；`requesterId` 可空 GUID；`items` 必填 1–200 项，每项 `productId` 必填、`quantity` ≥ 1、`unitPrice` 0–9999999.99；`remark` ≤ 200 |
| `GetPurchaseRequisitionsRequest` | `page ≥ 1`；`pageSize` 1–100；`keyword` ≤ 50 |

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── purchaseRequisition.ts
└── views/
    └── PurchaseRequisitionManagement/
        ├── PurchaseRequisitionsView.vue     # 列表
        ├── PurchaseRequisitionFormPage.vue  # 登记 / 编辑（共用）
        └── PurchaseRequisitionDetailView.vue# 详情（转采购订单 / 作废）
```

### 4.2 路由与菜单

| path | name | 组件 |
|---|---|---|
| `purchase-requisitions` | `purchaseRequisitions` | `PurchaseRequisitionsView` |
| `purchase-requisitions/new` | `purchaseRequisitionCreate` | `PurchaseRequisitionFormPage` |
| `purchase-requisitions/detail/:id` | `purchaseRequisitionDetail` | `PurchaseRequisitionDetailView` |

- 「采购」分组首位续行「采购请购」；`meta.permission = 'purchaseRequisitions.view'`。

### 4.3 页面交互

- **列表**：筛选（关键词 / 状态 / 申请人 / 日期区间）；列：请购单号、申请人、需求日期、期望到货、金额、状态（`a-tag`）、创建时间、操作列（查看 / 编辑 / 转采购订单 / 作废，按状态显示）。
- **表单页**：申请人（员工下拉）/ 需求日期 / 期望到货 / 明细子表（商品 pick、数量、预估单价）/ 备注；新建与编辑共用（编辑态由查询参数 `?id=` 表达）。
- **详情页**：基本信息 + 明细只读 + 「转采购订单」（成功后跳转采购订单详情或提示订单号）+ 「作废」；非草稿只读。

### 4.4 接口层

- `api/purchaseRequisition.ts`：列表 / 新建 / 详情 / 编辑 / 作废 / 转单；`REQUISITION_STATUS_META`（值 → 文案 / 颜色）。

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 |
|---|---|---|
| 复用 `037` 三态 + 一次性转单范式 | 不发明新模型 | 报价单已验证该范式，降低认知与实现成本 |
| 请购**不指定供应商** | 转订单时选 | 请购是需求，供应商选择属采购决策 |
| 不接入 `042` 审批 | 三态即可 | 请购非库存生效单；接审批需改 `042` 单据类型集合，本期避免 |
| 单号前缀取 `RQ` | 非 `PR` | `PR` 已被采购退货占用（`ROADMAP` §6.7） |
| 明细复制 `UnitPrice`（预估） | 保留预估价 | 便于日常参考；订单可改价，二者解耦 |
| 不做导出 / 打印 | 一期克制 | 请购为内部流程单，导出诉求低 |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

- **创建**：金额后端计算；申请人存在性 `40400`；商品存在性。
- **编辑 / 作废**：草稿可改；非草稿 `40183`。
- **转单**：草稿可转、同事务建订单 + 回写；非草稿 / 已转 `40184`；明细与金额复制正确；订单 `FlowStatus = Pending`。
- **列表**：筛选与明细行数聚合。
- **清单守卫**：6 个端点纳入 `028` 既有权限标注守卫。
