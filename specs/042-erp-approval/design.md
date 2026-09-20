---
created: 2026-09-17
updated: 2026-09-17
---

# 设计规格：大额单据审批（erp-approval）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织，以 `erp-purchase`（单据域模板）+ `023-erp-settlement`（状态类用例）为结构参照；字段约束单一来源（后端规则 §5.3）同样适用。
> **本规格改变单据生效时点**（`design.md` §0.2），并据此把四类单据的「生效动作」抽为共享组件（`design.md` §3.1），`015` / `016` / `021` / `022` 需留演进注记。

## 0. 审批约定（唯一事实源）

### 0.1 规则与状态

| 概念 | 规则 |
|---|---|
| 规则粒度 | 按**单据类型**（`PurchaseInbound` / `SalesOutbound` / `PurchaseReturn` / `SalesReturn`）一条规则：`ThresholdAmount`（阈值，> 0）+ `Enabled` |
| 触发条件 | 规则启用 **且** 单据 `TotalAmount >= ThresholdAmount` |
| 单据审批状态 | `ApprovalStatus`：`None = 0`（无需审批，默认）/ `Pending = 1`（待审批）/ `Approved = 2`（已通过）/ `Rejected = 3`（已驳回）/ `Withdrawn = 4`（已撤回） |
| 审批记录状态 | `ApprovalStatus` 复用同一枚举（`Pending` / `Approved` / `Rejected` / `Withdrawn`；记录不会为 `None`） |
| 单据「有效」定义 | `OrderStatus = Normal` **且** `ApprovalStatus ∈ {None, Approved}`；其余组合视为无效（列表置灰 / 不可作废） |
| 一张单据一条审批记录 | `Approvals` 唯一索引 `(OrderType, OrderId)`；驳回 / 撤回后**不再重新提交**（要重做就作废重开新单，符合「一步式、不可编辑」口径） |

### 0.2 生效时点（本规格的核心）

| 场景 | 库存 / 流水 / 成本 | 单据状态 |
|---|---|---|
| 未命中规则（未启用或金额 < 阈值） | **保存即生效**（既有行为，逐条不变） | `Status = Normal`、`ApprovalStatus = None` |
| 命中规则 | **保存时不生效**（不动库存、不写流水、不动成本） | `Status = Normal`（占位正常）、`ApprovalStatus = Pending` |
| 审批通过 | **审批通过时才生效**（同一事务内执行生效动作） | `ApprovalStatus = Approved` |
| 审批驳回 | 不生效（从未生效，**无库存回冲**） | `ApprovalStatus = Rejected`、`Status = Voided` |
| 提交人撤回 | 不生效（同上） | `ApprovalStatus = Withdrawn`、`Status = Voided` |
| 通过时生效失败（如库存不足） | 不做任何变更（整体回滚） | 保持 `ApprovalStatus = Pending`（可重试或驳回） |

- **待审批单据对下游不可见**：`025` 报表 / `019` 流水 / 库存数量均不含待审批单据（因为未生效）；单据列表**可见**（带「待审批」标签），避免「提交后凭空消失」。
- **待审批单据禁止**：编辑（四类单据本就不可编辑）、作废（`PUT .../void` → `40136`）；只能走审批 / 驳回 / 撤回。
- 生效动作内容与既有完全一致：库存增减（按仓 / 按批次，`038` / `040` 口径）+ 库存流水 + 成本更新（`026`）+ 审计（`029`）。

### 0.3 审批人与意见

| 项 | 规则 |
|---|---|
| 审批人 | 具备 `approvals.approve` 权限的用户（`028`） |
| 自审禁止 | 提交人 == 审批人 → `40137`（同一个人不得既提又批） |
| 审批意见 | 通过时可空、**驳回时必填**（≤ 200 字符） |
| 撤回 | 仅**提交人本人**可撤回，且单据处于 `Pending` |
| 提醒 | 提交时向 `approvals.approve` 用户发站内信（`041` 的 `INotificationWriter`，`NotificationType.ApprovalPending = 3`）；审批完成时向提交人发 `ApprovalDecided = 4` |

## 1. 总体设计

```
创建单据（改造四类创建用例）
  Create{PurchaseOrder|SalesOrder|PurchaseReturn|SalesReturn}
    → 既有校验与金额重算（不变）
    → IApprovalRuleRepository.GetAsync(orderType)（新增）
      ├─ 未命中 → <域>OrderFulfillment.ApplyAsync(...)（既有生效动作，抽为共享组件）→ 保存即生效
      └─ 命中   → 落单（ApprovalStatus = Pending）+ IApprovalRepository.AddAsync + 站内信 → 不生效

审批（前端 /approvals）
  → ApprovalsController
    → Features/Approvals/ApproveOrder | RejectApproval | WithdrawApproval
      → IApprovalRepository（记录）+ 对应单据仓储（取单 / 改审批状态）
      → 通过：<域>OrderFulfillment.ApplyAsync(...)（与创建路径共用同一生效逻辑，同一事务）
```

核心原则：

- **生效逻辑只有一份**：把「库存 + 流水 + 成本 + 审计」抽为按单据类型的共享生效组件（Core 内**按职责命名**、无 `*Service` 后缀，符合后端规则 §4.1「通用无状态技术组件按职责命名」），创建路径（未命中）与审批通过路径共用，杜绝两套生效逻辑漂移。
- **审批是闸门、不是新流程引擎**：只有「单级阈值 → 通过 / 驳回 / 撤回」，不做分支与多级（范围外）。
- **未命中即零变化**：规则默认 `Enabled = false`，升级后行为与升级前逐条一致（既有 e2e 全绿为验收门槛）。
- **失败不产生半截数据**：通过时的所有变更在同一 `IUnitOfWork` 事务内，失败整体回滚并保留 `Pending` 供重试。

## 2. 数据模型

> 时间字段统一 `DateTimeOffset` → `timestamptz`；枚举统一小整数 → `smallint`。

### 2.1 实体 `App.Core/Entities/ApprovalRule.cs` 与表 `ApprovalRules`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `OrderType` | `SettlementOrderType` | `smallint` | NOT NULL，唯一索引 | 单据类型（复用 `023` 枚举，语义泛化为「业务单据类型」；见 `032` §0.2 同一处理） |
| `ThresholdAmount` | `decimal` | `numeric(18,2)` | NOT NULL，> 0 | 审批阈值 |
| `Enabled` | `bool` | `boolean` | NOT NULL，默认 `false` | 是否启用 |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 审计字段 |
| `CreatedBy` / `UpdatedBy` | `Guid?` | `uuid` | NULL | 操作人 |

- 四类单据各一行（迁移不预置数据；`GetApprovalRules` 对缺失类型返回默认「未启用」行，`UpdateApprovalRules` 为 upsert）。

### 2.2 实体 `App.Core/Entities/Approval.cs` 与表 `Approvals`

| 字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | PK | |
| `OrderType` | `SettlementOrderType` | `smallint` | NOT NULL | 单据类型 |
| `OrderId` | `Guid` | `uuid` | NOT NULL | 被审批单据 id（唯一索引 `(OrderType, OrderId)`） |
| `OrderNo` | `string` | `varchar(20)` | NOT NULL | 单据号**快照** |
| `PartnerName` | `string` | `varchar(50)` | NOT NULL | 往来名称**快照** |
| `Amount` | `decimal` | `numeric(18,2)` | NOT NULL | 单据金额（触发时的快照） |
| `Status` | `ApprovalStatus` | `smallint` | NOT NULL，默认 `1` | §0.1（记录不会为 `None`） |
| `SubmittedBy` | `Guid` | `uuid` | NOT NULL | 提交人 |
| `SubmittedAt` | `DateTimeOffset` | `timestamptz` | NOT NULL | 提交时间 |
| `DecidedBy` | `Guid?` | `uuid` | NULL | 审批人 |
| `DecidedAt` | `DateTimeOffset?` | `timestamptz` | NULL | 审批时间 |
| `DecisionRemark` | `string?` | `varchar(200)` | NULL | 审批意见（驳回必填） |

- 索引：`(Status, SubmittedAt DESC)`（待我审批列表）、`(OrderType, OrderId)` 唯一、`(SubmittedBy)`。

### 2.3 四类单据表追加列

| 表 | 新增列 | 说明 |
|---|---|---|
| `PurchaseOrders`（`024` 后 `PurchaseReceipts`）/ `SalesOrders`（`024` 后 `SalesShipments`）/ `PurchaseReturns` / `SalesReturns` | `ApprovalStatus`（`smallint`，NOT NULL，默认 `0 = None`）+ 索引 `(ApprovalStatus)`（列表筛选） | 单据审批状态 |

- 四类单据的**出参 DTO 追加 `approvalStatus`**（列表 + 详情），前端展示标签；列表筛选追加 `approvalStatus`（可空）。

### 2.4 枚举与迁移

- 新增 `App.Core/Entities/ApprovalStatus.cs`：`None = 0` / `Pending = 1` / `Approved = 2` / `Rejected = 3` / `Withdrawn = 4`。
- `041` 的 `NotificationType` 续行：`ApprovalPending = 3` / `ApprovalDecided = 4`（原「逾期应收」预留值调整为 `5`，`041` 同步）。
- 迁移：`dotnet ef migrations add AddErpApproval -p src/App.Infrastructure -s src/App.Api`（两张新表 + 四张单据表加列；既有单据默认 `None`，行为不变）。
- 字段约束：`DecisionRemark` ≤ 200 引用 `OrderFieldConstraints.RemarkMaxLength`；阈值上下界引用 `ProductFieldConstraints.PriceMinValue/PriceMaxValue`；**不新建常量类**。

## 3. 后端设计

### 3.1 共享生效组件（本规格的关键重构）

| 组件 | 位置 | 说明 |
|---|---|---|
| `PurchaseReceiptFulfillment` | `App.Core/Features/Purchases/` | 采购入库生效：逐行 `IncrementAsync`（按仓 / 批次）+ 流水 + 成本 + 状态；入参为「单据 + 明细 + 操作人 + 时间 + 事务上下文」 |
| `SalesShipmentFulfillment` | `App.Core/Features/Sales/` | 销售出库生效：`TryDecrementAsync`（不足 → `40103`）+ 流水 + 成本 |
| `PurchaseReturnFulfillment` / `SalesReturnFulfillment` | `App.Core/Features/PurchaseReturns|SalesReturns/` | 退货生效（方向不同） |

- **命名说明**：组件按**职责**命名（`*Fulfillment`），不含 `Service` 后缀；不参与业务流程编排（只做「把这张单生效」这一件事），符合后端规则 §4.1 对 `*Service` 的限制。
- 每个组件的方法签名统一为 `Task ApplyAsync(<单据> order, IReadOnlyList<<明细>> items, Guid? operatorId, DateTimeOffset utcNow, CancellationToken ct)`，**不自行 `Commit`**（事务由调用方 `IUnitOfWork` 控制）。
- 改造既有创建用例：`Create*` 的生效段替换为调用该组件（**行为逐条不变**，为纯重构，由既有单测与 e2e 守护）。

### 3.2 仓储接口（新增）

`IApprovalRuleRepository`：

| 方法 | 说明 |
|---|---|
| `Task<IReadOnlyList<ApprovalRule>> GetAllAsync(...)` | 全部规则（列表页用） |
| `Task<ApprovalRule?> GetAsync(SettlementOrderType orderType, ...)` | 取某类型规则（创建时判定） |
| `Task UpsertAsync(SettlementOrderType orderType, decimal threshold, bool enabled, Guid? operatorId, DateTimeOffset utcNow, ...)` | 保存规则（不存在则建） |

`IApprovalRepository`：

| 方法 | 说明 |
|---|---|
| `Task AddAsync(Approval, ...)` | 新增审批记录 |
| `Task<(IReadOnlyList<Approval> Items, int Total)> GetPagedAsync(ApprovalStatus? status, SettlementOrderType? orderType, Guid? submittedBy, DateTimeOffset? start, DateTimeOffset? end, int page, int pageSize, ...)` | 列表（`SubmittedAt DESC`；「待我审批」= `status = Pending`） |
| `Task<Approval?> GetByIdAsync(Guid id, ...)` | 详情 |
| `Task<Approval?> GetByOrderAsync(SettlementOrderType orderType, Guid orderId, ...)` | 按单据取（列表 / 详情展示审批信息） |
| `Task UpdateDecisionAsync(Guid id, ApprovalStatus status, Guid decidedBy, DateTimeOffset decidedAt, string? remark, ...)` | 通过 / 驳回 / 撤回 |

- 四类单据仓储追加 `Task UpdateApprovalStatusAsync(Guid id, ApprovalStatus status, ...)`（原子更新）。

### 3.3 错误码（追加到 `App.Core/Errors/ErrorCode.cs`）

| code | 常量 | 含义 |
|---:|---|---|
| 40136 | `ApprovalStateInvalid` | 单据 / 审批记录当前状态不允许该操作（非待审批、待审批单据不可作废等） |
| 40137 | `ApprovalSelfForbidden` | 不可审批自己提交的单据 |

> 复用：`40103 InsufficientStock`（通过时库存不足，审批保持待审批）、`40104 OrderVoided`、`40400` / `40000`、`40300`（无审批权限）。

### 3.4 用例与接口（每 API 一个用例，均经 `IMediator.Send`）

| 接口 | 方法 | 用例目录 | `data` 响应 | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/approvals` | GET | `Approvals/GetApprovals` | `PagedResult<ApprovalListItemDto>` | `approvals.view` / 40000 |
| `/api/approvals/{id:guid}` | GET | `Approvals/GetApprovalById` | `ApprovalDetailDto`（含被审批单据摘要与明细） | `approvals.view` / 40400 |
| `/api/approvals/{id:guid}/approve` | PUT | `Approvals/ApproveOrder` | `ApprovalDetailDto` | `approvals.approve` / 40103 / 40104 / 40136 / 40137 / 40400 |
| `/api/approvals/{id:guid}/reject` | PUT | `Approvals/RejectApproval` | `ApprovalDetailDto` | `approvals.approve` / 40136 / 40137 / 40400 |
| `/api/approvals/{id:guid}/withdraw` | PUT | `Approvals/WithdrawApproval` | `ApprovalDetailDto` | `approvals.view`（仅本人）/ 40136 / 40400 |
| `/api/approval-rules` | GET | `Approvals/GetApprovalRules` | `IReadOnlyList<ApprovalRuleDto>` | `approvals.rules` / 40000 |
| `/api/approval-rules` | PUT | `Approvals/UpdateApprovalRules` | `IReadOnlyList<ApprovalRuleDto>` | `approvals.rules` / 40000 |

- 单据侧改造（无新端点）：四类单据列表 / 详情出参追加 `approvalStatus`；列表请求追加 `approvalStatus` 筛选；`void` 端点对待审批单据返回 `40136`。

### 3.5 关键用例流程（Handler）

**CreatePurchaseOrder（改造）**：
1. 既有校验（往来 / 商品 / 明细 / 金额重算）与单号生成不变。
2. 取规则：`GetAsync(PurchaseInbound)`；**未命中**（记录不存在 / `Enabled = false` / `TotalAmount < ThresholdAmount`）→ `IUnitOfWork` 事务内：`AddAsync`（单据 + 明细，`ApprovalStatus = None`）→ `PurchaseReceiptFulfillment.ApplyAsync(...)` → `CommitAsync`（**与改造前行为一致**）。
3. **命中** → `IUnitOfWork`：`AddAsync`（`ApprovalStatus = Pending`）→ `IApprovalRepository.AddAsync`（`Pending`，含单据号 / 往来名 / 金额 / 提交人 / 时间）→ `CommitAsync` → 站内信（`INotificationWriter`，给 `approvals.approve` 用户，失败仅记日志不影响单据）→ 返回详情（前端提示「已提交审批，等待审批后生效」）。

**ApproveOrder**：
1. 取审批记录（不存在 → `40400`）→ `Status != Pending` → `40136`。
2. `DecidedBy == SubmittedBy` → `40137`。
3. 取被审批单据（不存在 → `40400`；`Status = Voided` → `40104`）→ 校验 `ApprovalStatus = Pending`（否则 `40136`）。
4. `IUnitOfWork`：`BeginTransactionAsync` → `Fulfillment.ApplyAsync(...)`（可能抛 `40103` / `40107` 等 → 整体回滚，审批保持 `Pending`）→ 单据 `UpdateApprovalStatusAsync(Approved)` → `UpdateDecisionAsync(Approved, ...)` → `CommitAsync`。
5. 站内信通知提交人（`ApprovalDecided`）。

**RejectApproval**：取记录（`40400` / `40136`）→ `40137` → 意见必填（Validator）→ 事务内：单据 `UpdateApprovalStatusAsync(Rejected)` + `UpdateStatusAsync(Voided)` + `UpdateDecisionAsync(Rejected, remark)` → `CommitAsync` → 通知提交人。**不做任何库存操作**（从未生效）。

**WithdrawApproval**：取记录（`40400` / `40136`）→ `SubmittedBy != 当前用户` → `40400`（不暴露他人记录）→ 事务内：单据 `Withdrawn` + `Voided` + 记录 `Withdrawn` → `CommitAsync`。

**GetApprovals / GetApprovalById**：筛选（状态 / 类型 / 提交人 / 时间）+ 映射；详情附带被审批单据的表头与明细（按 `OrderType` 分派仓储读取，只读展示）。

**GetApprovalRules / UpdateApprovalRules**：读取四类规则（缺失返回默认未启用）；保存为逐类型 upsert（阈值 > 0，`Enabled` 布尔）。

### 3.6 校验规则（FluentValidation，仅格式层，引用既有常量）

| 请求 | 规则 |
|---|---|
| `RejectApprovalRequest` | `remark` 必填 1–200（`OrderFieldConstraints.RemarkMaxLength`） |
| `ApproveApprovalRequest` | `remark` 可空 ≤200 |
| `GetApprovalsRequest` | `page ≥ 1`；`pageSize` 1–100；`status` / `orderType` / `submittedBy` 可空合法值；`start` / `end` 可空且 `start <= end` |
| `UpdateApprovalRulesRequest` | `rules` 必填 1–4 项、`orderType` 不重复；每项 `thresholdAmount` > 0 且 ≤ 9999999.99；`enabled` 布尔 |
| 四类 `Create*Request` | 不变（审批不新增入参） |

### 3.7 Swagger

- **不分组**（同既有约定）：7 个新增接口按现有方式出现在单文档 Swagger 中。

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── approval.ts                     # 审批接口层 + 状态文案与颜色映射常量
└── views/
    └── ApprovalManagement/
        ├── ApprovalsView.vue           # 审批列表（「待我审批」/「全部」tab）
        ├── ApprovalDecideDrawer.vue    # 审批抽屉（通过 / 驳回 + 意见 + 单据摘要与明细）
        └── ApprovalRulesDrawer.vue     # 审批规则设置（4 行阈值 + 启用开关）
```

- 改造既有页面（不新建）：四类单据列表（审批状态列 + 筛选）与详情（审批状态描述项 + 待审批时的「撤回」按钮 + 提示文案）。

### 4.2 接口层

- `src/api/approval.ts`：7 个接口 + 类型 + `approvalStatus` 文案与颜色映射（全项目唯一来源，供审批页与单据页共用）。
- 四类单据 `api/<域>.ts` 的类型追加 `approvalStatus`；列表 query 追加 `approvalStatus`。

### 4.3 路由与菜单

| path | name | 组件 |
|---|---|---|
| `approvals` | `approvals` | `ApprovalsView` |

- `AppLayout.vue`「系统」分组追加「单据审批」（`025` §0.2 总表已预留 `approvals`）；菜单项 `permission: 'approvals.view'`。

### 4.4 页面交互

**审批列表 `ApprovalsView.vue`**（参照 `specs/006-list-showcase/design.md` §0）：

- `a-tabs`：「待我审批」（默认，`status = Pending`）/「全部」；切换 tab 重置筛选与页码。
- 筛选行：单据类型下拉 + 提交人（`a-select`，可空）+ 时间范围 + 搜索 / 重置（「待我审批」tab 下状态固定为待审批）。
- 操作行：「审批规则」（`approvals.rules` 权限，打开规则抽屉）+ 刷新 + 列设置。
- 列：序号、单据类型（`a-tag`）、单据号、往来单位、金额、提交人、提交时间、状态（`a-tag` §0.1）、操作列（「审批」（`IconCheck`，仅 `Pending` 显示）/「详情」）；服务端分页。

**审批抽屉 `ApprovalDecideDrawer.vue`**（宽 640）：被审批单据摘要（`a-descriptions`：单号 / 往来 / 金额 / 日期）+ 单据明细只读表格 + 审批意见（`a-textarea` ≤200）+ 底部「通过」（primary，`approving`）/「驳回」（danger，`rejecting`，意见必填）；通过时二次确认 popconfirm（不可撤销）；**通过成功或失败都刷新列表**（失败时保留 drawer 并展示后端错误，如库存不足）。

**审批规则抽屉 `ApprovalRulesDrawer.vue`**：4 行（单据类型 + 阈值 `a-input-number` + 启用 `a-switch`）+ 底部保存（`submitting`）；提示「阈值以上需审批；未启用则该类单据保存即生效」。

**单据页改造**：列表追加「审批状态」列（`None` 显示 `-`）与筛选；详情追加审批状态描述项与「撤回」（仅 `Pending` 且提交人本人，popconfirm + `withdrawing`）；待审批单据隐藏「作废」按钮；待审批时页面顶部 `a-alert info`「该单据待审批，审批通过后才会产生库存变动」。

### 4.5 按钮 loading（遵循 `specs/010-button-loading/design.md` §0）

| 操作 | 状态 | 绑定 |
|---|---|---|
| 审批列表查询 | `loading` | 搜索 / 翻页 / tab 切换 + 表格 |
| 通过 / 驳回 | `approving` / `rejecting` | 抽屉底部按钮（+ 防重入） |
| 撤回 | `withdrawing` | 单据详情 / 列表按钮 |
| 保存审批规则 | `submitting` | 规则抽屉保存按钮 |

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 / 取舍 |
|---|---|---|
| 待审批**不生效**（而非先占库存后审批） | 审批通过才动库存 | 「先占库存」会让库存在审批期间被锁定、下游（销售）看不到可用量却能感觉到缺货，语义混乱；不生效最简单且与「作废即回冲」口径一致 |
| 审批通过时执行生效（而非另开生效单） | 复用共享生效组件 | 不新增单据类型；`Create*` 与 `Approve` 共用同一生效逻辑，避免两套实现漂移（这是本规格最大的重构点） |
| 生效组件按职责命名（无 `Service` 后缀） | `*Fulfillment` | 后端规则 §4.1 禁止参与业务编排的 `*Service`，但允许「通用无状态技术组件按职责命名」；本组件只做「把这张单生效」 |
| 规则默认不启用 | `Enabled = false` + 迁移不预置 | 升级后行为逐条不变（既有 e2e 全绿）；由管理员按需开启，避免上线即拦住全部大额单据 |
| 通过失败保留待审批 | `40103` 等 → 回滚、状态 `Pending` | 审批期间库存可能被其他单据消耗（销售场景）；自动作废会让审批人的操作变成隐式作废，交由人决定（驳回或稍后重试） |
| 驳回 / 撤回 → 单据作废 | `ApprovalStatus = Rejected/Withdrawn` + `Status = Voided` | 单据从未生效，无库存回冲；置作废可让列表/报表口径干净（不产生「永远待审」的僵尸单据） |
| 不可自审 | `40137` | 审批的意义在于第二双眼睛；自审等于无审批（单级模型下必须硬拦） |
| 不重新提交 | 驳回后需重开单 | 四类单据不可编辑（`015` §5），重新提交需要「撤销审批 + 改单」两条能力；一步式单据下「作废重开」业务上等价且模型更简 |
| 撤回仅提交人 | 归属校验 | 提交人改主意属正常场景；其他人撤回会绕过审批职责 |
| 只有单级阈值 | 不做多级 / 分支 | 多级与条件路由需要审批流引擎（组织模型 + 规则表达式），属独立能力（范围外） |
| 盘点 / 调拨不纳入 | 无业务金额 | 金额阈值对无价格单据无意义；如需要按数量审批另立（范围外） |
| 站内信只发一次 | 提交时 + 决定时 | 不做过期升级与重复提醒（`041` 不提供），避免打扰 |
| 单据类型枚举复用 | `SettlementOrderType` | 同形枚举重复定义要避免（`038` / `032` 同一处理）；若实施时统一改名 `BusinessOrderType`，三处同步 |
| 审批状态与订单状态分列 | `ApprovalStatus` + `OrderStatus` | 两者正交（审批可驳回且作废），合并会导致状态组合爆炸（`024` 对订单状态的同类取舍） |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

> Mock 仓储接口（含 `IApprovalRepository` / `IApprovalRuleRepository` / `INotificationWriter`）；`TestCurrentUser` 同既有约定；时间用固定 `DateTimeOffset` 入参或注入时钟，不读 `DateTime.Now`。

- **创建路径（四类各一例）**：
  - 未命中（规则不存在 / `Enabled = false` / 金额 < 阈值）→ 断言 `Fulfillment` 被调用（库存 / 流水 / 成本一次到位）、`ApprovalStatus = None`、未调用 `IApprovalRepository.AddAsync`；
  - 命中 → 断言**未调用任何库存 / 流水 / 成本方法**、单据 `ApprovalStatus = Pending`、审批记录参数正确（单据号 / 往来名 / 金额 / 提交人）、站内信写入、`Commit`。
- **`ApproveOrder`**：成功（断言 `Fulfillment` 被调用一次 + 单据与记录状态更新 + 通知提交人 + `Commit`）；`40136`（非 `Pending` / 单据状态不符）；`40137`（自审）；`40400`（记录 / 单据不存在）；`40104`（单据已作废）；**生效失败**（`TryDecrementAsync` 返回 false → `40103`，断言 `RollbackAsync`、记录仍为 `Pending`）。
- **`RejectApproval`**：成功（意见必填校验 `40000`；单据 `Rejected + Voided`；**无任何库存调用**）；`40137`；`40136`。
- **`WithdrawApproval`**：成功（提交人本人）；他人 → `40400`；非 `Pending` → `40136`。
- **规则用例**：`GetApprovalRules`（缺失类型返回默认未启用）；`UpdateApprovalRules` upsert（新类型创建 / 已存在更新）；阈值边界 0.01 通过 / 0 拒绝 / 1e7 拒绝；`orderType` 重复 `40000`。
- **单据侧改造**：`Void*` 对待审批单据 → `40136`；列表 `approvalStatus` 筛选传参；出参含 `approvalStatus`；未命中阈值路径的既有断言**逐条不变**（回归守护）。
- **共享生效组件重构**：既有 `Create*` 单测（库存 / 流水 / 成本 / 事务）在重构后**全部保持通过**（这是纯重构的验收门槛）。
- **字段约束一致性**（扩展 `FieldValidationConsistencyTests`）：`Approvals.OrderNo` 20 == `OrderFieldConstraints.OrderNoMaxLength`；`DecisionRemark` 200 == `RemarkMaxLength`；`Approvals` 唯一索引 `(OrderType, OrderId)` 存在（EF 模型断言）。
