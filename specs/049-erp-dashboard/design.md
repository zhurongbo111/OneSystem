---
created: 2026-10-07
updated: 2026-10-07
---

# 设计规格：经营分析看板（erp-dashboard）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织；本规格为**只读聚合**（1 个用例 + 1 个只读仓储），不改动任何写入路径。
> 指标口径**不重复定义**：只登记「指标 → 来源规格 §」指针；数据来源口径见 `specs/025-erp-report/design.md` §0.1、`specs/026-erp-cost/design.md` §0、`specs/023-erp-settlement/design.md` §0、`specs/034-erp-cash/design.md` §0、`specs/036-erp-partner-price/design.md` §0.3。

## 0. 约定正文（唯一事实源）

### 0.1 指标口径（指针登记，不复制算法）

**区间额**（`start <= 日期 < end`，与 `025` §0.1 半开区间一致；均只计**未作废且已生效**单据，待审批不计）：

| 指标 | 来源口径 |
|---|---|
| 销售净额 | `025` §0.1 金额口径「销售净额」（销售出库 − 销售退货） |
| 采购净额 | `025` §0.1「采购净额」 |
| 毛利额 / 毛利率 | `026` 成本与毛利口径（毛利额 = 销售净额 − 销售成本；毛利率 = 毛利额 ÷ 销售净额，分母 0 时按 0） |

**时点值**（截至 `end` 当日，`end` 缺省为今天）：

| 指标 | 来源口径 |
|---|---|
| 库存数量 / 库存金额 | `025` §0.1 库存余额表口径（金额按 `026` 成本） |
| 应收未结 / 应付未结 | `023` §0 未结金额（`TotalAmount − SettledAmount`，四表归集） |
| 逾期应收（笔数 / 金额） | `036` §0.3 逾期判据（到期日 < 基准日 且 未结清） |
| 资金余额 | `034` §0 派生余额（初始余额 + Σ 收款 − Σ 付款，只计未作废） |
| 待审批单据数 | `042` 待审批（`ApprovalStatus = Pending`）计数 |
| 库存预警数 | `041` §0 信号数（低库存 + 近效期 + 过期，口径见该规格） |

**趋势 / TOP**：

| 项 | 口径 |
|---|---|
| 月度趋势 | 近 **12 个月**（含当前月）逐月「销售净额 / 采购净额」，口径同「区间额」 |
| TOP 商品 | 区间内按商品汇总销售净额，降序取前 **10**（商品维度，`025` §0.1 分组维度） |
| TOP 客户 | 区间内按客户汇总销售净额，降序取前 **10**（往来维度） |

- **基准日 / 区间由入参传入**：仓储不读系统时间（可测性，同 `025` / `036`）；`start` / `end` 缺省由 Handler 计算（本月 1 号 → 今天）。
- **空数据**：所有指标缺省 0 / 空数组，不报错。

### 0.2 菜单归属（在 `025` §0.2 表续行）

「报表（`report`）」分组续行一首位子项：

| 顶级分组 | 子项（key） | 引入规格 |
|---|---|---|
| 报表（`report`） | 经营看板（`dashboard`） | `049` |

### 0.3 权限点（在 `028` §0.2 表续行）

| 域 | key 前缀 | 权限点（动作） | 对应接口 / 页面 |
|---|---|---|---|
| 经营看板 | `reports`（复用） | 复用 `reports.view` | `/api/dashboard`、`/dashboard` |

- **不新增权限点**：看板属只读分析，与报表同权限（`028` §0.2 无需改表，只在本节登记复用）。

## 1. 总体设计

```
经营看板（前端 /dashboard，域目录 Dashboard/）
  → DashboardController
    → App.Core/Features/Dashboard/GetDashboard/*RequestHandler
      → IDashboardQueryRepository（新增，只读跨表聚合）
        → PostgreSQL（SalesShipments / SalesReturns / PurchaseReceipts / PurchaseReturns
                        / StockMovements / Inventory / Settlements / Vouchers / BankAccounts
                        / Approvals / Notifications）
```

核心原则：

- **只汇集、不重算**：所有指标复用既有口径的 SQL 形态，看板不引入新算法；口径漂移风险由 `design.md` §0.1 的指针登记约束。
- **一个用例、一次取数**：单接口聚合返回，避免前端多次请求（KPI + 趋势 + TOP 一并返回）。
- **入参注入基准**：`start` / `end` 由 Handler 计算并下传，仓储不读系统时间。

## 2. 数据模型

- **无新表、无新列、无迁移**（纯只读聚合）。

## 3. 后端设计

### 3.1 仓储接口（`App.Core/Abstractions/IDashboardQueryRepository.cs`）

| 方法 | 说明 |
|---|---|
| `GetPeriodAmountsAsync(start, end, ...)` | 区间：销售净额 / 采购净额 / 销售成本（→ 毛利） |
| `GetPointInTimeAsync(asOf, ...)` | 时点：库存数量 / 金额、应收 / 应付未结、逾期应收（笔 / 额）、资金余额、待审批数、预警数 |
| `GetMonthlyTrendAsync(months, anchor, ...)` | 近 N 月（含 `anchor` 当月）逐月销售 / 采购净额 |
| `GetTopAsync(start, end, dimension, take, ...)` | TOP 商品 / 客户（`dimension` 枚举，`take = 10`） |

- 读模型（`App.Core/Abstractions/`，`sealed record`）：`DashboardPeriodAmounts`、`DashboardPointInTime`、`DashboardTrendPoint`、`DashboardTopItem`。
- 所有查询沿用既有报表仓储的 SQL 形态（`025` 的 `IReportQueryRepository` / `026` 成本 / `023` 未结 / `034` 余额），按需复用或并行取数。

### 3.2 错误码

- **无新增错误码**（只读聚合，复用 `40000`）；`ROADMAP` §6「下一个可用」保持 `40181`。

### 3.3 用例与接口

| 接口 | 方法 | 用例目录 | `data` | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/dashboard` | GET | `Dashboard/GetDashboard` | `DashboardDto`（KPI + 趋势 + TOP） | `reports.view` / 40000 |

- `GetDashboardRequest`：`start` / `end`（可空 ISO 日期）。

### 3.4 关键用例流程（Handler）

**GetDashboard**：`start` / `end` 缺省补全（本月 1 号 → 今天）→ 并行调 §3.1 四组查询 → `DashboardDtoMapper.ToDashboardDto(...)` 组装（毛利额 = 销售净额 − 销售成本；毛利率分母 0 → 0）。

### 3.5 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `GetDashboardRequest` | `start` / `end` 可空；非空时合法日期且 `start <= end`（否则 `40000`） |

## 4. 前端设计

### 4.1 目录

```
src/
├── api/
│   └── dashboard.ts                    # 看板接口与类型
└── views/
    └── Dashboard/
        └── DashboardView.vue           # 看板单页（KPI + 趋势 + TOP）
```

### 4.2 路由与菜单

| path | name | 组件 |
|---|---|---|
| `dashboard` | `dashboard` | `DashboardView` |

- 「报表」分组续行首位「经营看板」；`meta.permission = 'reports.view'`。

### 4.3 页面交互

- **`DashboardView.vue`**：顶部区间选择（本月 / 本季 / 本年 / 自定义 `a-range-picker`）；KPI 卡片区（销售净额 / 采购净额 / 毛利额与率 / 库存数量与金额 / 应收 / 应付 / 逾期应收 / 资金余额 / 待审批 / 预警数）；中部两张趋势图（销售 / 采购月度）；底部两张 TOP 表（商品 / 客户）。
- 卡片数值格式：金额千分位两位小数；比例按 `025` §0.1 的 `FormatRatio` 口径。
- 空数据展示 0 / 空图占位；图表选型遵循前端规则 §4.7（图标已定 Tabler，图表库随项目既有选型）。

### 4.4 接口层

- `api/dashboard.ts`：`getDashboard(params)`；`DashboardDto` 等类型与后端 DTO camelCase 一一对应。

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 |
|---|---|---|
| 单接口聚合 | `GET /api/dashboard` | 一屏数据一次取齐；避免多接口并发与前端拼装 |
| 口径**指针登记**而非复制 | §0.1 | 遵守单一事实源（`AGENTS.md` §10）；口径改动只改来源规格 |
| 复用 `reports.view` | 不新增权限点 | 看板与报表同属"只读分析"，细分收益不足 |
| 挂「报表」分组首位 | 而非新顶级分组 | 遵循 `025` §0.2「后续规格只在本表续行」；避免菜单分组增殖 |
| 固定布局 | 不做 BI 化 | 收益集中在"一屏总览"，自定义拖拽是另一个产品（§5 范围外） |
| 不做下钻 | 卡片为只读展示 | 既有明细页已可查；下钻会引入路由与参数耦合 |
| 空数据不报错 | 缺省 0 | 新账套 / 新区间可用性优先 |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

- **区间额**：`start` / `end` 半开区间边界；作废 / 待审批剔除（与 `025` 一致）；销售净额 = 出库 − 退货。
- **时点值**：库存金额、应收 / 应付未结、逾期应收、资金余额、待审批 / 预警数各与来源口径一致。
- **趋势**：12 个月序列长度与月度归属；跨年边界。
- **TOP**：降序前 10、并列处理、金额为 0 的过滤。
- **缺省**：`start` / `end` 缺省为本月；空账套全 0。
- **校验**：`start > end` → `40000`。
