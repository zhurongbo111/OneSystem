---
created: 2026-09-17
updated: 2026-09-17
---

# 任务清单：库存预警通知（erp-stock-alert）

> 依据 `specs/035-erp-stock-alert/design.md` 拆分。含后端扫描能力 + 定时宿主 + 5 个用例 + 前端顶栏铃铛与站内信列表。**先做扫描与站内信，再做宿主**（宿主最简单且最难测，放最后）。
> 前置：`028`（权限点 `inventory.view`、`IPermissionResolver`）与 `030`（仓级安全库存）已实现；近效期 / 过期信号依赖 `032`（若 `032` 未落地，阶段 B 可延后）。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与仓储

- [ ] 1.1 新增枚举 `NotificationType` / `AlertType`；实体 `Notification` / `AlertRecord`；`NotificationFieldConstraints`
- [ ] 1.2 EF 配置（`Notifications` 索引、`AlertRecords` 唯一索引 `(AlertType, ResourceKey, AlertDate)`）+ `AppDbContext` 2 个 `DbSet`
- [ ] 1.3 增量迁移 `dotnet ef migrations add AddErpStockAlert -p src/App.Infrastructure -s src/App.Api`
- [ ] 1.4 `INotificationRepository` + 实现（批量写 / 本人列表 / 未读数 / 单条已读 / 全部已读）+ 注册
- [ ] 1.5 `IAlertRecordRepository` + 实现（`ExistsAsync` / `AddRangeAsync`）+ 注册
- [ ] 1.6 `IStockAlertQueryRepository` + 实现（低库存信号 / 近效期信号 / 过期信号；含 `maxCount` 上限）
- [ ] 1.7 `IPermissionedUserQuery` + 实现（按权限点取启用用户，复用 `028` 的权限数据）
- [ ] 1.8 `StockAlertFieldConstraints`（`MaxSignalsPerScan = 500` 等）

## 二、后端：扫描能力

- [ ] 2.1 `INotificationWriter` + 实现（批量写站内信，供扫描器与 `036` 复用）
- [ ] 2.2 `IStockAlertScanner` + `App.Core/Alerts/StockAlertScanner.cs`（三类信号 → 去重 → 生成消息 → 统计；文案模板）
- [ ] 2.3 `App.Core/DependencyInjection.cs` 注册扫描器与相关组件

## 三、后端：用例与接口

- [ ] 3.1 共享出参 `Features/Notifications/NotificationListItemDto.cs` / `NotificationSummaryDto.cs` / `StockAlertScanResultDto.cs` + `NotificationsDtoMapper.cs`
- [ ] 3.2 新增用例 `Notifications/GetNotifications`、`GetNotificationSummary`、`MarkNotificationRead`、`MarkAllNotificationsRead`、`ScanStockAlerts`
- [ ] 3.3 `NotificationsController`（5 端点，固定段在 `{id:guid}` 之前）+ DI 注册
- [ ] 3.4 权限点：`028` §0.2 续行 `notifications.scan`；接口按 `notifications.view` / `notifications.scan` 标注

## 四、后端：定时宿主

- [ ] 4.1 `StockAlertBackgroundService`（延迟首跑 / 周期 / 异常隔离 / `ILogger`）
- [ ] 4.2 配置节 `StockAlert`（`Enabled` / `IntervalMinutes` / `StartupDelaySeconds`）+ `appsettings.json` 默认值；`Enabled = false` 时不注册
- [ ] 4.3 时钟处理：宿主传 `DateTimeOffset.UtcNow` 给扫描器（扫描器不读时钟）

## 五、单元测试（后端）

- [ ] 5.1 扫描正常：三类信号 + 多接收人 → 消息与台账数量正确、统计正确
- [ ] 5.2 去重：同日二次扫描跳过；跨天可再告警；无接收人时不写台账
- [ ] 5.3 判定口径：阈值 0 / 库存 = 阈值 / 近效期 30 与 31 天边界 / 已过期不重复算近效期 / 库存 0 不告警
- [ ] 5.4 上限：超 `MaxSignalsPerScan` 只处理前 500 条
- [ ] 5.5 站内信用例：只看本人 / 筛选分页 / 未读汇总 / 已读与非本人 `40400` / 全部已读条数 / 手动扫描统计
- [ ] 5.6 配置：`Enabled = false` 不注册宿主；间隔下限规范化
- [ ] 5.7 字段约束一致性（列长 / `keyword` / 唯一索引存在）
- [ ] 5.8 `cd backend && dotnet build` / `dotnet test` 通过（既有用例全部回归）

## 六、前端

- [ ] 6.1 `src/api/notification.ts`（5 个接口 + 类型 + 类型文案与颜色映射）
- [ ] 6.2 `views/NotificationManagement/NotificationsView.vue`（筛选 + 表格 + 查看跳转 + 全部已读 + 立即扫描 + 分页）
- [ ] 6.3 `AppLayout.vue` 顶栏铃铛（未读 `a-badge` + `a-popover` 最近 5 条 + 「查看全部」；点击单条标记已读并跳转）
- [ ] 6.4 路由新增 `notifications`；`router.afterEach` 刷新未读数；§0.2 总表标注「顶栏入口、不进侧边菜单」
- [ ] 6.5 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 七、E2E（Playwright）

- [ ] 7.1 新增 `e2e/notification.spec.ts`：制造低库存 → 站内信页「立即扫描」→ 列表出现「低库存」消息、铃铛未读数 +1
- [ ] 7.2 同文件：点击消息跳转库存查询并带筛选；标记已读后未读数 -1；「全部已读」后未读为 0
- [ ] 7.3 同文件：同日再次扫描不新增（列表条数不变）
- [ ] 7.4 同文件：无 `inventory.view` 权限的用户（`028` 建的角色）收不到库存告警（登录该用户 → 铃铛无未读）
- [ ] 7.5 `cd frontend && npm run test:e2e` 全量通过（含 `inventory-management` / `warehouse` / `rbac` 既有用例回归）

## 八、规格与上下文联动

- [ ] 8.1 `specs/030-erp-multi-warehouse/design.md` §0 加注记：低库存告警消费仓级阈值口径
- [ ] 8.2 `specs/032-erp-batch-expiry/design.md` §0 加注记：近效期 / 过期告警消费 `NearExpiryDays` 与过期判定
- [ ] 8.3 `specs/028-erp-rbac/design.md` §0.2 续行：`notifications.scan`
- [ ] 8.4 `specs/033-erp-partner-price/design.md` §5 加注记：催收提醒所需的站内信通道已由 `035` 交付（规则后续）
- [ ] 8.5 `specs/036-erp-approval/` 引用 `INotificationWriter` 生成待审批通知（在 `036` tasks 联动）
- [ ] 8.6 `.codebuddy/CONTEXT.md` §2（Notifications / AlertRecords 实体与仓储、`IStockAlertScanner`、宿主）、§3（NotificationManagement 域、api 文件、顶栏铃铛）、§6 同步
- [ ] 8.7 `specs/ROADMAP.md` 状态列更新（`035` → 已实现）

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 扫描逻辑单测覆盖三类信号与去重（不依赖定时宿主）；手动扫描与定时扫描共用同一实现（单测断言）。
