---
created: 2026-09-17
updated: 2026-09-23
---

# 任务清单：业务操作审计日志（erp-audit-log）

> 依据 `specs/029-erp-audit-log/design.md` 拆分。含单表 + 写入抽象 + **各域写路径接入（范围最广）** + 2 个只读接口 + 前端列表 / 详情。**按阶段顺序实现，每阶段跑全量测试**。
> 前置：`028`（权限点 `auditLogs.view`）已实现；`012`–`027` 各写用例已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：模型与写入基建

- [x] 1.1 新增 `AuditResource` / `AuditAction` 枚举；实体 `AuditLog`；`AuditLogFieldConstraints`
- [x] 1.2 EF 配置 `AuditLogConfiguration`（列长 / `jsonb` / 索引 `CreatedAt DESC` / `(Resource, ResourceId)` / `UserId` / `ResourceNo`）+ `AppDbContext` 注册
- [x] 1.3 增量迁移 `dotnet ef migrations add AddErpAuditLog -p src/App.Infrastructure -s src/App.Api`
- [x] 1.4 `App.Core/Audit/`：`AuditEntry` / `AuditChangeBuilder`（只记变化 + 敏感字段黑名单 + 截断）/ `AuditSummary`（格式化辅助）
- [x] 1.5 `IAuditLogger` + `App.Infrastructure/Audit/AuditLogger.cs`（`Add` + `SaveChanges`，操作人经 `ICurrentUser`）+ 注册
- [x] 1.6 `IAuditLogRepository` + 实现（`AddAsync` / `GetPagedAsync`（列表投影排除 `Changes`）/ `GetByIdAsync`）+ 注册

## 二、后端：查询用例与接口

- [x] 2.1 共享出参 `Features/AuditLogs/AuditLogListItemDto.cs` / `AuditLogDetailDto.cs` + `AuditLogsDtoMapper.cs`（`Changes` 反序列化 + 坏 JSON 兜底）
- [x] 2.2 新增用例 `AuditLogs/GetAuditLogs`、`AuditLogs/GetAuditLogById`
- [x] 2.3 `AuditLogsController`（2 端点，`auditLogs.view`）+ DI 注册

## 三、后端：写入接入（按 design §3.6 逐域）

- [x] 3.1 基础档案：商品（创建 / 更新 / 启停）、分类（创建 / 更新 / 删除）、往来单位（创建 / 更新 / 启停）
- [x] 3.2 用户与权限：用户（创建 / 更新 / 启停 / 重置密码 / 角色变更）、角色（创建 / 更新 / 删除）
- [x] 3.3 采购域：采购订单（创建 / 更新 / 作废 / 关闭）、采购入库（创建 / 作废）、采购退货（创建 / 作废）
- [x] 3.4 销售域：销售订单（创建 / 更新 / 作废 / 关闭）、销售出库（创建 / 作废）、销售退货（创建 / 作废）
- [x] 3.5 资金与库存：收付款（创建核销 / 作废）、库存盘点（期初 / 盘点）
- [x] 3.6 运维：成本重算（Recalculate）
- [x] 3.7 每处接入遵循「业务写之后、`CommitAsync` 之前」且 `utcNow` 取自既有用例时间参数

## 四、单元测试（后端）

- [x] 4.1 `AuditChangeBuilder`：只记变化 / 敏感字段拦截 / 集合快照 / 截断 / JSON 可反序列化
- [x] 4.2 各域接入点参数断言（资源 / 动作 / 业务标识 / 摘要 / 变化字段），至少覆盖 design §3.6 每域一例
- [x] 4.3 失败路径不写日志（`40101` / `40103` / `40104` / `40119` 各一例）
- [x] 4.4 `Commit` 异常 → `RollbackAsync`（日志随业务回滚）
- [x] 4.5 `GetAuditLogs` 筛选 / 分页 / 列表不加载 `Changes`；`GetAuditLogById` 反序列化 / 空差异 / `40400` / 坏 JSON 兜底
- [x] 4.6 范围表守卫测试（§0.1 登记组合均有日志接入）
- [x] 4.7 字段约束一致性（`Summary` 200 / `ResourceNo` 50 / `keyword` 50-51）
- [x] 4.8 `cd backend && dotnet build` / `dotnet test` 通过（既有用例全部回归）

## 五、前端

- [x] 5.1 `src/api/auditLog.ts`（2 个查询 + 类型 + 资源 / 动作文案与颜色映射常量）
- [x] 5.2 `views/AuditLogManagement/AuditLogsView.vue`（筛选行 + 只读表格 + 服务端分页 + 详情入口 + `loading`）
- [x] 5.3 `views/AuditLogManagement/AuditLogDetailDrawer.vue`（`a-descriptions` + 字段级差异表格 + 无差异空态 + `detailLoading`）
- [x] 5.4 `router/index.ts` 新增 `audit-logs`（`meta.permission = 'auditLogs.view'`）；`AppLayout.vue`「系统」分组追加「操作日志」
- [x] 5.5 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 六、E2E（Playwright）

- [x] 6.1 新增 `e2e/audit-log.spec.ts`：改商品采购价 → 日志出现记录，详情含「采购价 10.00 → 8.80」
- [x] 6.2 同文件：作废采购入库单 → 日志动作「作废」且业务标识为单号
- [x] 6.3 同文件：业务失败（重复商品编码）→ 日志无新增记录
- [x] 6.4 同文件：筛选（资源类型 / 动作 / 关键词）与空状态
- [x] 6.5 `cd frontend && npm run test:e2e` 全量通过

## 七、规格与上下文联动

- [x] 7.1 `specs/009-user-management/design.md` 加注记：登录日志（`UserLoginLogs`）与操作日志（`AuditLogs`）职责区分
- [x] 7.2 `specs/012`–`028` 各 `design.md` 加注记：写操作已接入操作日志（指针到 `029` §0.1）
- [x] 7.3 `.codebuddy/CONTEXT.md` §2（AuditLogs 实体 / 枚举 / 仓储 / `IAuditLogger`）、§3（AuditLogManagement 域、api 文件）、§6 同步
- [x] 7.4 `specs/ROADMAP.md` 状态列更新（`029` → 已实现）

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 范围表守卫通过：`design.md` §0.1 登记的每个「资源 × 动作」均有日志接入与用例覆盖。
