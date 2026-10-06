---
created: 2026-09-17
updated: 2026-10-06
---

# 任务清单：大额单据审批（erp-approval）

> 依据 `specs/042-erp-approval/design.md` 拆分。含**单据生效重构（共享生效组件）+ 审批闸门 + 2 张新表 + 7 个用例 + 前端审批页与单据页状态**。**阶段顺序不可颠倒**：先做「纯重构」（行为不变），再加审批闸门。
> 前置：`028`（`approvals.*` 权限点、审批人）与 `041`（`INotificationWriter`、`NotificationType`）已实现；`015` / `016` / `021` / `022` 已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、阶段 A：生效逻辑重构（纯重构，行为零变化）

- [x] 1.1 抽出 `PurchaseReceiptFulfillment` / `SalesShipmentFulfillment` / `PurchaseReturnFulfillment` / `SalesReturnFulfillment`（入参含单据 / 明细 / 操作人 / 时间 / 明细读取；不自行 Commit）
- [x] 1.2 四类 `Create*` 用例改用共享组件（删除原内联生效逻辑）；**既有单测与 e2e 必须逐条保持通过**（本阶段是纯重构，结束即验证）
- [x] 1.3 `cd backend && dotnet test` + `npm run test:e2e` 全绿（阶段门禁：行为零变化）——后端 `dotnet test` 1187 通过；e2e 全量 188 通过（见第六节）

## 二、阶段 B：数据模型与规则

- [x] 2.1 新增枚举 `ApprovalStatus`；实体 `ApprovalRule` / `Approval`；EF 配置（`ApprovalRules` 唯一 `OrderType`、`Approvals` 唯一 `(OrderType, OrderId)` 与索引）+ `AppDbContext` 2 个 `DbSet`
- [x] 2.2 四类单据实体 / 配置追加 `ApprovalStatus`（默认 `None`）+ 索引
- [x] 2.3 增量迁移 `dotnet ef migrations add AddErpApproval -p src/App.Infrastructure -s src/App.Api`（`20261006092051_AddErpApproval`）
- [x] 2.4 `ErrorCode.cs` 追加 `40136 ApprovalStateInvalid` / `40137 ApprovalSelfForbidden`
- [x] 2.5 `IApprovalRuleRepository` / `IApprovalRepository` + 实现（含 `GetByOrderAsync` / `UpdateDecisionAsync`）+ 注册；四类单据仓储追加 `UpdateApprovalStatusAsync`（`GetPagedAsync` 追加 `approvalStatus` 筛选）

## 三、阶段 C：审批闸门与用例

- [x] 3.1 四类 `Create*` 命中规则时改为「落单 Pending + 审批记录 + 站内信，不生效」；未命中保持原路径
- [x] 3.2 四类 `Void*` 对待审批单据返回 `40136`
- [x] 3.3 新增用例 `Approvals/GetApprovals`、`GetApprovalById`、`ApproveOrder`、`RejectApproval`、`WithdrawApproval`、`GetApprovalRules`、`UpdateApprovalRules`
- [x] 3.4 共享出参 `Features/Approvals/ApprovalDto.cs`（列表 / 明细 / 详情 / 规则）+ `ApprovalsDtoMapper.cs`
- [x] 3.5 `ApprovalsController`（`/api/approvals` 5 端点 + 绝对路由 `/api/approval-rules` 2 端点）+ DI 注册（用例 / 校验器 / 4 个生效组件 / `ApprovalNotifier` / `ApprovalDetailBuilder` / `ApprovalOrderCloser`）
- [x] 3.6 四类单据列表 / 详情 DTO 追加 `approvalStatus`；列表请求追加 `approvalStatus` 筛选
- [x] 3.7 `041` 的 `NotificationType` 续行 `ApprovalPending = 3` / `ApprovalDecided = 4`（逾期应收预留值改为 5）；`042` 的 `design.md` §2.4 与 `041` 同步（含 `041` 单测的取值边界断言）

## 四、单元测试（后端）

- [x] 4.1 创建路径四例：未命中（生效一次到位、无审批记录）/ 命中（**无任何库存与流水调用**、Pending、审批记录、站内信）
- [x] 4.2 `ApproveOrder`：成功 / 非 Pending `40136` / 自审 `40137` / `40400` / 单据已作废 `40104` / 库存不足 `40103`（回滚且保持 Pending）
- [x] 4.3 `RejectApproval`（意见必填、单据 Rejected + Voided、无库存调用、`40137`、`40136`）与 `WithdrawApproval`（本人成功 / 他人 `40400` / 非 Pending `40136`）
- [x] 4.4 规则用例：缺失返回默认、upsert、阈值与去重边界
- [x] 4.5 单据侧：`Void*` 待审批 `40136`、列表筛选与出参、未命中路径既有断言回归
- [x] 4.6 字段约束一致性（`OrderNo` 20 / `DecisionRemark` 200 / 唯一索引存在）
- [x] 4.7 `cd backend && dotnet build` / `dotnet test` 通过（既有用例全部回归：1187 通过）

## 五、前端

- [x] 5.1 `src/api/approval.ts`（7 个接口 + 类型 + 状态文案与颜色映射）
- [x] 5.2 `views/ApprovalManagement/ApprovalsView.vue`（待我审批 / 全部 tab + 筛选 + 审批与详情入口 + 分页）
- [x] 5.3 `views/ApprovalManagement/ApprovalDecideDrawer.vue`（单据摘要 + 明细只读 + 意见 + 通过 / 驳回 + popconfirm + 防重入 + 失败保留）
- [x] 5.4 `views/ApprovalManagement/ApprovalRulesDrawer.vue`（4 行阈值 + 启用开关 + 保存 + 提示文案）
- [x] 5.5 四类单据列表追加「审批状态」列与筛选；详情追加状态项、「撤回」按钮（仅本人且 Pending）、待审批时隐藏作废并显示提示 `a-alert`
- [x] 5.6 四类 `api/<域>.ts` 类型与 query 追加 `approvalStatus`
- [x] 5.7 `router/index.ts` 新增 `approvals`；`AppLayout.vue`「系统」分组追加「单据审批」
- [x] 5.8 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿
- [x] 5.9 （落地补充）`api/notification.ts` 扩展 `NotificationType` 与 `NOTIFICATION_TYPE_META`（3 待审批 / 4 审批结果）——否则站内消息页因类型取值为 `undefined` 整表渲染失败（`041` §0 的续行注记即指此处）

## 六、E2E（Playwright）

- [x] 6.1 新增 `e2e/approval.spec.ts`：配置规则（采购入库阈值，见下）→ 开大额采购单 → 单据「待审批」且**库存不变**
- [x] 6.2 同文件：审批通过 → 库存增加、流水出现、单据变正常；审批页「待我审批」清空该条
- [x] 6.3 同文件：小额单据直接生效（无审批状态）；驳回大额单据 → 单据「已作废 / 已驳回」且库存不变
- [x] 6.4 同文件：自审被拒（提示可见）；提交人撤回 → 单据作废且库存不变
- [x] 6.5 同文件：站内信（`041`）出现「待审批」消息并可跳转审批页（复用 `notification` 用例的断言方式）
- [x] 6.6 既有采购 / 销售 / 退货 spec 回归（规则未启用 → 行为与改造前一致）
- [x] 6.7 `cd frontend && npm run test:e2e` 全量通过（188 通过）

> **e2e 阈值取值说明（`6.1`）**：规则是**全局配置**（不按用户 / 单据隔离），而 e2e 全量并行共享同一临时库、多数 spec 靠「开采购单入库」造库存。故用例把阈值取为 `1000000`（远高于既有 spec 的采购金额），并在文件末尾 `afterAll` 复位为「全部不启用」，避免污染其余用例——**这正是 `6.6`（规则未启用时行为逐条不变）的守护方式**。单测仍以 1000 / 1500 覆盖边界语义（`design.md` §6）。

## 七、规格与上下文联动

- [x] 7.1 `specs/015` / `016` / `021` / `022` `design.md` 加「演进（erp-approval）」注记：生效时点（命中阈值时延迟到审批通过）、生效逻辑抽为共享 `*Fulfillment` 组件、单据追加 `ApprovalStatus`、待审批禁止作废
- [x] 7.2 `specs/019` / `026` `design.md` 加注记：待审批单据不写流水、不动成本（生效时才写）
- [x] 7.3 `specs/025-erp-report/design.md` §0.1 加注记：报表口径不含未生效单据（审批通过前不进入报表）
- [x] 7.4 `specs/028-erp-rbac/design.md` §0.2 续行：`approvals.rules`
- [x] 7.5 `specs/029-erp-audit-log/design.md` §0.1 续行：提交审批 / 通过 / 驳回 / 撤回摘要
- [x] 7.6 `specs/041-erp-stock-alert/design.md` §2.1 与 §0：`NotificationType` 续行待审批与审批结果；「逾期应收」预留值调整为 5
- [x] 7.7 `specs/ROADMAP.md` §6.7 单号前缀表无需变更（不新增单据类型）
- [x] 7.8 `.codebuddy/CONTEXT.md` §2（Approvals / ApprovalRules 实体与仓储、`*Fulfillment` 组件、错误码）、§3（ApprovalManagement 域、api 文件）、§6 同步
- [x] 7.9 `specs/ROADMAP.md` 状态列更新（`042` → 已实现）

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 两条硬口径成立：① 待审批单据在审批通过前**不产生任何库存 / 流水 / 成本变化**；② 规则未启用时四类单据行为与改造前**逐条一致**。
