---
created: 2026-10-07
updated: 2026-10-07
---

# 设计规格：逾期应收提醒（erp-receivable-alert）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织；本规格为 `041` 的**能力扩展**（新增 1 个扫描器 + 复用站内信），**不改动任何既有写入路径**。
> 逾期判据不重复定义：见 `specs/036-erp-partner-price/design.md` §0.3；站内信 / 去重台账与宿主见 `specs/041-erp-stock-alert/design.md` §0 / §2 / §3。

## 0. 约定正文（唯一事实源）

### 0.1 逾期应收信号与去重

| 项 | 取值 / 判定 |
|---|---|
| 信号类型 | `AlertType.ReceivableOverdue = 3`（`041` §2.3 续行） |
| 通知类型 | `NotificationType.ReceivableOverdue = 5`（`041` §2.1 的**预留值转生产**） |
| 数据来源 | **销售出库单**（`SalesShipments`，`GI`）：未作废、未结清 |
| 逾期判定 | 复用 `036` §0.3：`到期日 = 单据日期 + 客户账期`；`到期日 < 今天` 且 `未结金额 = TotalAmount − SettledAmount > 0`；账期 `0`（现结）即 `到期日 = 单据日期`，**次日即逾期** |
| `ResourceKey` | `receivable:order:<orderId>`（一单一键） |
| `AlertDate` | **`到期日 + 1 天`（首次逾期日，恒定）**——使唯一索引 `(AlertType, ResourceKey, AlertDate)` 天然保证**同一单据只提醒一次** |
| 接收人 | 具备 `reconciliation.view` 的**启用**用户（`041` 的 `IPermissionedUserQuery`；`SuperAdmin` 视为具备） |
| 跳转 | `LinkRouteName = reconciliation`、`LinkQuery = {"overdueOnly":"true"}` |

- **一次性语义**：`AlertDate` 恒定（不随扫描当日变化），故同一单据永远只命中一次去重键；**结清后不再产生新信号**（未结金额为 0 即不命中）。
- **内容模板**（固定中文）：「客户 A 的销售出库单 GI202608010001（金额 10,000.00）已逾期 15 天，请及时催收」。

### 0.2 扫描结果语义

- 每条信号 × 每个接收人 = 一条站内信；返回值在 `041` §0.2 计数基础上追加应收侧「信号数 / 生成消息数 / 跳过（去重）数 / 接收人数」。
- **无外部推送**；发送失败只记日志，不影响业务数据（与 `041` / `042` 一致）。

### 0.3 权限点与菜单

- **无新增权限点**：接收人筛选复用 `reconciliation.view`（`028` §0.2 已登记），页面复用 `notifications.view`。
- **无新增菜单**：仅站内消息页类型文案扩充（`041` 已有页面）。

## 1. 总体设计

```
定时扫描（复用 041 宿主）
  StockAlertBackgroundService（同一 tick）
    → IStockAlertScanner.ScanAsync(utcNow)（既有）
    → IReceivableAlertScanner.ScanAsync(utcNow)（本规格，App.Core/Alerts/）
      → IReceivableAlertQueryRepository（只读：逾期未结销售出库单）
      → IAlertRecordRepository.ExistsAsync / AddAsync（去重）
      → IPermissionedUserQuery（reconciliation.view 启用用户）
      → INotificationWriter（批量写站内信）

手动触发（前端「立即扫描」）
  POST /api/notifications/scan → Notifications/ScanStockAlerts（改造：两扫描器一并执行）
```

核心原则：

- **判据复用、口径单一**：逾期判据引用 `036` §0.3，本规格不重新定义；`utcNow` 由入口注入（可测性，同 `041`）。
- **不刷屏**：一次性去重（§0.1），不做周期催收。
- **只读 + 追加**：只读单据 / 客户 / 权限，只写 `Notifications` 与 `AlertRecords`，**不改任何业务数据**。
- **宿主与手动共用实现**：不维护第二套扫描逻辑。

## 2. 数据模型

- **无新表、无新列**：完全复用 `041` 的 `Notifications` 与 `AlertRecords`。
- **枚举续行**（`041` §2.3）：
  - `AlertType` 追加 `ReceivableOverdue = 3`（原 0–2 不变）；
  - `NotificationType` 的 `ReceivableOverdue = 5` 由**预留转为生产**（`042` 已占 3 / 4，取值不变；追加 / 转正均**不涉及迁移**）。
- **迁移**：无（仅枚举值与代码）。

## 3. 后端设计

### 3.1 只读查询接口（`App.Core/Abstractions/`）

| 接口 / 方法 | 说明 |
|---|---|
| `IReceivableAlertQueryRepository.GetOverdueReceivablesAsync(DateOnly today, CancellationToken)` | 返回**信号行**：`ResourceKey` / 单号 / 客户名 / 金额 / 到期日 / 逾期天数。SQL：`SalesShipments`（未作废、`TotalAmount − SettledAmount > 0`）⋈ `Partners`（取 `PaymentTermDays`、`Status` 启用）；到期日 `单据日期 + 账期` 在**仓储侧按 `AddDays` 计算并下推 SQL**（逐单扫描可下推，区别于 `036` 往来对账的内存聚合） |

- 读模型：`ReceivableAlertSignal`（`sealed record`：`ResourceKey` / `OrderNo` / `PartnerName` / `Amount` / `DueDate` / `OverdueDays`；`Abstractions/`）。
- 时间基准 `today` 由用例传入（仓储不读系统时间，同 `036` §3.1）。

### 3.2 扫描器（`App.Core/Alerts/ReceivableAlertScanner.cs`）

| 类型 | 位置 | 说明 |
|---|---|---|
| `IReceivableAlertScanner` | `App.Core/Alerts/` | `Task<ReceivableAlertScanResult> ScanAsync(DateTimeOffset utcNow, CancellationToken)` |
| `ReceivableAlertScanner` | `App.Core/Alerts/` | ① `GetOverdueReceivablesAsync(today)` 取信号；② 逐条 `ExistsAsync(ReceivableOverdue, resourceKey, alertDate=到期日+1)` → 跳过或 `AddAsync`；③ 取接收人 → `INotificationWriter.AddRangeAsync`；④ 汇总计数。注册于 `AddCore` |

- 复用 `041` 的 `IPermissionedUserQuery` 与 `INotificationWriter`；异常隔离（单信号失败不影响其余）。

### 3.3 宿主与用例改造

| 组件 | 变更 |
|---|---|
| `StockAlertBackgroundService`（`App.Api/HostedServices/`） | 同一 tick 内先跑库存扫描、再跑应收扫描（各自 try/catch 隔离）；配置节沿用 `StockAlert`（`Enabled = false` 时不注册） |
| `Notifications/ScanStockAlerts`（用例） | 增注入 `IReceivableAlertScanner`；一次调用执行两个扫描器，Response 增应收侧计数（`OverdueSignalCount` / `OverdueMessageCount` / `OverdueSkippedCount` / `OverdueReceiverCount`） |

- 端点 / 权限点不变（`POST /api/notifications/scan`，`notifications.scan`）。

### 3.4 错误码

- **不新增业务码**（复用 `40000` / `40400` / `40300`）；`ROADMAP` §6「下一个可用」保持 `40176`。

### 3.5 校验规则

- 本规格无新增请求体（扫描端点参数不变），无新增 Validator。

## 4. 前端设计

### 4.1 目录（仅改造 `041` 既有文件）

```
src/
├── api/
│   └── notification.ts           # NOTIFICATION_TYPE_META 增类型 5（文案 / 颜色）
└── views/
    └── NotificationManagement/
        └── NotificationsView.vue # 类型筛选下拉增「逾期应收」项
```

### 4.2 页面交互

- **`NotificationsView.vue`**：类型筛选 `a-select` 增「逾期应收」；列表类型标签按 `NOTIFICATION_TYPE_META` 渲染（`orange`）；点击跳转「往来对账」页并按 `LinkQuery.overdueOnly=true` 过滤。
- 「立即扫描」按钮文案与结果提示：结果消息追加应收侧计数（如「应收逾期：新增 3 条」）。

### 4.3 接口层

- `api/notification.ts`：`NOTIFICATION_TYPE_META` 增 `5`（「逾期应收」）；扫描返回类型增应收侧计数字段。
- 跳转目标复用既有 `reconciliation` 路由（`SettlementManagement/ReconciliationView.vue`），**不新增页面**。

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 |
|---|---|---|
| 仅做**应收** | 销售出库未结 | 与 `036` 账期 / 授信同侧；应付提醒场景不同（我方主动付款），另立评估 |
| **一次性**而非周期提醒 | `AlertDate = 到期日 + 1` | 每日提醒会刷屏；周期催收需"催收周期 / 升级"模型，收益不足（§5 范围外） |
| 去重复用 `AlertRecords` | 不新建台账 | 唯一索引已是通用去重结构；`ResourceKey` 前缀区分业务 |
| `NotificationType = 5` 转生产 | 不改值 | `041` 已预留该值，转正零迁移 |
| 复用同一宿主 / 端点 | 不新建宿主 | 扫描入口唯一，运维与 e2e 一致（同 `041` §1 原则） |
| `today` 由入口注入 | 仓储不读系统时间 | 可单测、基准日可复现（同 `036`） |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

- **查询**：账期 0 → 到期日 = 单据日期；`AddDays` 边界（到期当天不逾期、次日逾期）；已结清 / 已作废剔除；客户停用剔除。
- **扫描器**：命中生成消息；`ExistsAsync` 命中即跳过（一次性）；接收人取 `reconciliation.view` 启用用户；无接收人 → 跳过并记日志。
- **宿主 / 用例**：一次调用同时得到库存与应收计数；单扫描器异常不影响另一个。
- **枚举**：`AlertType` / `NotificationType` 取值与 `041` §2 一致（无重复值）。
