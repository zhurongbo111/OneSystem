---
created: 2026-10-07
updated: 2026-10-07
---

# 任务清单：逾期应收提醒（erp-receivable-alert）

> 依据 `specs/047-erp-receivable-alert/design.md` 拆分。扩展 `041`：新增应收扫描器 + 复用站内信 / 去重台账 / 定时宿主 + 前端类型文案 + e2e。
> 前置：`036`（账期 / 到期日）、`041`（站内信 / `AlertRecords` / 宿主）、`023`（未结金额）、`028`（权限）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：枚举与只读查询

- [ ] 1.1 `AlertType` 追加 `ReceivableOverdue = 3`；`NotificationType.ReceivableOverdue = 5` 预留转生产（`041` §2.3 续行）
- [ ] 1.2 读模型 `ReceivableAlertSignal`（`App.Core/Abstractions/`）
- [ ] 1.3 `IReceivableAlertQueryRepository.GetOverdueReceivablesAsync(today)` + 实现（SQL `AddDays` 下推、未结清 / 未作废 / 客户启用过滤）+ 注册

## 二、后端：扫描器

- [ ] 2.1 `IReceivableAlertScanner` + `ReceivableAlertScanner`（`App.Core/Alerts/`）：信号 → 去重（`AlertDate = 到期日 + 1`）→ 接收人（`reconciliation.view`）→ `INotificationWriter` → 计数
- [ ] 2.2 DI 注册（`AddCore`）
- [ ] 2.3 复用 `041` 的 `IPermissionedUserQuery` / `INotificationWriter` / `IAlertRecordRepository`（无重复实现）

## 三、后端：宿主与用例

- [ ] 3.1 `StockAlertBackgroundService` 同一 tick 内增跑应收扫描（异常隔离）
- [ ] 3.2 `Notifications/ScanStockAlerts` 增注入并执行应收扫描；Response 增应收侧计数
- [ ] 3.3 确认端点 / 权限点不变（`POST /api/notifications/scan`，`notifications.scan`）

## 四、单元测试（后端）

- [ ] 4.1 查询：账期 0 边界、到期当天 / 次日、已结清 / 已作废 / 客户停用剔除
- [ ] 4.2 扫描：命中生成、去重一次性、接收人筛选、无接收人跳过
- [ ] 4.3 宿主 / 用例：两扫描器计数汇总、异常隔离
- [ ] 4.4 字段 / 枚举一致性：`041` §2 取值无重复
- [ ] 4.5 `cd backend && dotnet build` / `dotnet test` 通过（含 `041` / `042` 回归）

## 五、前端

- [ ] 5.1 `api/notification.ts`：`NOTIFICATION_TYPE_META` 增类型 `5`；扫描返回类型增应收计数字段
- [ ] 5.2 `views/NotificationManagement/NotificationsView.vue`：类型筛选增「逾期应收」；跳转 `reconciliation` + `overdueOnly=true`；扫描结果提示增应收计数
- [ ] 5.3 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 六、E2E（Playwright）

- [ ] 6.1 新增 `e2e/receivable-alert.spec.ts`：造逾期未结单据 → 手动扫描 → 站内信「逾期应收」出现 → 点击跳转对账页「仅看逾期」
- [ ] 6.2 同文件：再次扫描不新增（一次性）；结清后不提醒
- [ ] 6.3 `cd frontend && npm run test:e2e` 全量通过（含通知 / 审批域回归）

## 七、规格与上下文联动

- [ ] 7.1 `specs/041-erp-stock-alert/design.md` §0 / §2.1 / §2.3 续行 `AlertType.ReceivableOverdue` 与 `NotificationType = 5` 转生产；刷新其 `updated`
- [ ] 7.2 `specs/036-erp-partner-price/design.md` §0.3 增「逾期应收提醒复用本判据」指针
- [ ] 7.3 `.codebuddy/CONTEXT.md` §2（`Alerts/`、枚举 `NotificationType` / `AlertType`）/ §3（通知域）同步
- [ ] 7.4 `specs/ROADMAP.md` §4.2 状态更新（`047` → 已实现）；§3 覆盖矩阵「L1 平台 · 审批 / 通知」缺口同步；§6 下一个可用保持 `40176`

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 逾期判据与 `036` §0.3 一致；同一单据仅提醒一次；扫描不改动任何业务数据。
