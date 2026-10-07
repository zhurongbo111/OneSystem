---
created: 2026-10-07
updated: 2026-10-07
---

# 任务清单：经营分析看板（erp-dashboard）

> 依据 `specs/049-erp-dashboard/design.md` 拆分。只读聚合：1 个只读仓储 + 1 个用例 + 看板前端 + e2e。
> 前置：`025`（报表口径）、`026`（成本毛利）、`023`（应收应付）、`034`（资金余额）、`036` / `047`（逾期）、`041` / `042`（待办）、`028`（权限）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：只读聚合

- [ ] 1.1 读模型 `DashboardPeriodAmounts` / `DashboardPointInTime` / `DashboardTrendPoint` / `DashboardTopItem`（`App.Core/Abstractions/`）
- [ ] 1.2 `IDashboardQueryRepository`（`GetPeriodAmountsAsync` / `GetPointInTimeAsync` / `GetMonthlyTrendAsync` / `GetTopAsync`）+ 实现 + 注册
- [ ] 1.3 复用既有报表 / 成本 / 未结 / 余额查询（不复制算法，口径指针见 §0.1）

## 二、后端：用例与接口

- [ ] 2.1 `Dashboard/GetDashboard`（四件套）：缺省区间补全 + Mapper 组装（毛利率分母 0 → 0）
- [ ] 2.2 `DashboardDto` + `DashboardDtoMapper`
- [ ] 2.3 `DashboardController`（`GET /api/dashboard`，`reports.view`）+ DI 注册

## 三、单元测试（后端）

- [ ] 3.1 区间额：半开边界、作废 / 待审批剔除、销售净额 = 出库 − 退货
- [ ] 3.2 时点值：库存金额、应收 / 应付、逾期应收、资金余额、待审批 / 预警数与来源一致
- [ ] 3.3 趋势：12 月序列与跨年边界
- [ ] 3.4 TOP：降序前 10、并列、0 值过滤
- [ ] 3.5 缺省区间与空账套全 0；`start > end` → `40000`
- [ ] 3.6 `cd backend && dotnet build` / `dotnet test` 通过

## 四、前端

- [ ] 4.1 `api/dashboard.ts`
- [ ] 4.2 `views/Dashboard/DashboardView.vue`（区间选择 + KPI 卡片 + 趋势图 + TOP 表）
- [ ] 4.3 `router/index.ts` 新增路由；`AppLayout.vue`「报表」分组续行「经营看板」+ `MENU_ROUTE_MAP`
- [ ] 4.4 金额 / 比例格式化与 `025` §0.1 口径一致
- [ ] 4.5 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 五、E2E（Playwright）

- [ ] 5.1 新增 `e2e/dashboard.spec.ts`：建数据 → 看板 KPI 与来源页数值一致；区间切换趋势 / TOP 变化
- [ ] 5.2 同文件：空账套显示 0 不报错
- [ ] 5.3 `cd frontend && npm run test:e2e` 全量通过（含报表域回归）

## 六、规格与上下文联动

- [ ] 6.1 `specs/025-erp-report/design.md` §0.2 菜单续行「经营看板」；刷新其 `updated`
- [ ] 6.2 `specs/028-erp-rbac/design.md` §0.2 登记 `reports.view` 复用（不新增权限点）
- [ ] 6.3 `.codebuddy/CONTEXT.md` §2（读模型 / 仓储）/ §3（新增域 `Dashboard/`、菜单、api 文件）同步
- [ ] 6.4 `specs/ROADMAP.md` §4.2 状态更新（`049` → 已实现）；§3 覆盖矩阵「L5 分析 · 报表」缺口同步

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 看板指标与来源页数值一致；空数据可用；无任何写入路径改动。
