---
created: 2026-10-07
updated: 2026-10-07
---

# 设计规格：个税与社保（erp-payroll-tax）

> 遵循 `AGENTS.md`（统一响应 §4、错误码 §4.2、分页 §4.3、认证 §4.6、测试 §6）与后端 / 前端专项规则。
> 按后端规则 §4「分层架构（每 API 一个用例）」组织；本规格**扩展** `044-erp-hcm-payroll`（工资单加列 + 个税计算器 + 1 个用例，改造批量生成）。
> 工资单结构 / 状态 / 发放锁定见 `specs/044-erp-hcm-payroll/design.md` §0 / §2.2 / §3。

## 0. 约定正文（唯一事实源）

### 0.1 工资单字段与实发公式（**修订 `044` §0.1**）

| 项 | 规则 |
|---|---|
| 应发 | `BaseSalary + Allowance` |
| 税前扣除 | `SocialInsurance`（个人社保）+ `SpecialDeduction`（专项附加扣除） |
| 税后扣款 | `Deduction`（其它扣款，**不作税前扣除**） |
| 个税 | `Tax`（系统按 §0.2 计算） |
| **实发** | `NetPay = BaseSalary + Allowance − SocialInsurance − Tax − Deduction`（`numeric(18,2)`，后端计算） |
| 发放锁定 | `Paid` 禁止编辑 / 删除 / 重算个税（`40171` 复用，`044` §0.1） |

### 0.2 个人所得税口径（累计预扣预缴法，居民工资薪金）

| 项 | 规则 |
|---|---|
| 累计收入 | `Σ(本年度 1..M 月 应发)`（含本期 M） |
| 累计减除费用 | `5000 × M`（M = 本期月份序号） |
| 累计专项扣除 | `Σ(本年度 1..M 月 SocialInsurance)` |
| 累计专项附加扣除 | `Σ(本年度 1..M 月 SpecialDeduction)` |
| 累计应纳税所得额 `T` | `max(0, 累计收入 − 累计减除费用 − 累计专项扣除 − 累计专项附加扣除)` |
| 累计应缴税额 | `T × 预扣率(T) − 速算扣除数(T)`（按 §0.2.1 表） |
| 本期 `Tax` | `max(0, 累计应缴税额 − 累计已预扣预缴税额)`（累计已预扣 = 本年度 1..M−1 月 `Tax` 之和） |

**约定**：累计数据取该员工本年度**已存在**的工资单（缺月按 0 计）；税率 / 速算扣除数按**累计应纳税所得额**落入区间取值。

#### 0.2.1 预扣率表（唯一事实源）

| 级数 | 累计应纳税所得额（元） | 预扣率 | 速算扣除数 |
|---:|---|---:|---:|
| 1 | ≤ 36,000 | 3% | 0 |
| 2 | 36,000 < x ≤ 144,000 | 10% | 2,520 |
| 3 | 144,000 < x ≤ 300,000 | 20% | 16,920 |
| 4 | 300,000 < x ≤ 420,000 | 25% | 31,920 |
| 5 | 420,000 < x ≤ 660,000 | 30% | 52,920 |
| 6 | 660,000 < x ≤ 960,000 | 35% | 85,920 |
| 7 | > 960,000 | 45% | 181,920 |

- 税率表为**代码常量**（`PayrollTaxRateTable`），不落库、不可配置（§5 范围外）。

### 0.3 权限点与菜单

- **无新增权限点**：复用 `payroll.view` / `create` / `update` / `delete` / `status`（`028` §0.2）；个税计算用 `payroll.update`。
- **无新增菜单 / 路由**：仍在「人事 → 薪酬」（`025` §0.2）。

### 0.4 批量生成语义（修订 `044` §3.4）

- `GeneratePayrolls` 生成时：`SocialInsurance` / `SpecialDeduction` / `Deduction` 默认 `0`，`Tax` 按 §0.2 自动计算，`NetPay` 按 §0.1 计算。
- 生成后可在 `Draft` 状态下逐条调整社保 / 专项附加 / 其它扣款并**重算个税**。

## 1. 总体设计

```
薪酬（前端 /payrolls，域目录 HrmManagement/，与 044 同域）
  → PayrollsController（改造 + 1 新端点）
    → App.Core/Features/Payrolls/<Action>/*RequestHandler
      → PayrollTaxCalculator（纯计算，§0.2）
      → IPayrollRepository（扩：GetYearToDateAsync）
        → PostgreSQL（Payrolls）
```

核心原则：

- **计算是纯函数**：`PayrollTaxCalculator` 不依赖仓储 / 时钟，只吃入参（累计数据 + 本期数据），独立可测。
- **累计口径单一**：累计数据由 `IPayrollRepository.GetYearToDateAsync` 单点取数，计算公式只在本 §0.2 定义。
- **复用锁定**：已发放不可改（`044` 既有 `40171`），本规格不改该语义。

## 2. 数据模型

### 2.1 实体 `Payroll` 扩展（表 `Payrolls`）

| 新字段 / 列 | C# 类型 | 列类型 | 约束 | 说明 |
|---|---|---|---|---|
| `SocialInsurance` | `decimal` | `numeric(18,2)` | NOT NULL，默认 0，≥ 0 | 个人社保（税前扣除） |
| `SpecialDeduction` | `decimal` | `numeric(18,2)` | NOT NULL，默认 0，≥ 0 | 专项附加扣除 |
| `Tax` | `decimal` | `numeric(18,2)` | NOT NULL，默认 0，≥ 0 | 个税（系统计算） |

- `Deduction` 语义**明确**为「税后其它扣款」（`044` §2.2 修订）；`NetPay` 公式见 §0.1。

### 2.2 常量类

| 常量类 | 常量 |
|---|---|
| `PayrollFieldConstraints`（扩 `044`） | 复用 `AmountMaxValue`；新增字段沿用同上界 |
| `PayrollTaxRateTable`（`App.Core/Features/Payrolls/`） | 预扣率表（§0.2.1）+ `BasicDeductionPerMonth = 5000` |

### 2.3 迁移

- 迁移：`dotnet ef migrations add AddErpPayrollTax -p src/App.Infrastructure -s src/App.Api`（`Payrolls` 加 3 列，默认 0 回填）。

## 3. 后端设计

### 3.1 仓储与计算器

| 类型 / 方法 | 说明 |
|---|---|
| `IPayrollRepository.GetYearToDateAsync(employeeId, year, uptoMonth, ...)`（追加） | 返回本年度 1..M 工资单（`Tax` / `SocialInsurance` / `SpecialDeduction` / 应发）供累计 |
| `PayrollTaxCalculator.Calculate(ytd, current)`（新增） | 纯函数：按 §0.2 返回本期 `Tax`（入参：累计收入 / 累计社保 / 累计专项附加 / 累计已缴税 / 本期月份） |

### 3.2 错误码

- **无新增业务码**：已发放不可改复用 `40171`（`044`）；字段越界 `40000`。`ROADMAP` §6「下一个可用」保持 `40192`。

### 3.3 用例与接口

| 接口 | 方法 | 用例目录 | `data` | 权限点 / 错误码 |
|---|---|---|---|---|
| `/api/payrolls/generate`（改造） | POST | `Payrolls/GeneratePayrolls` | `IReadOnlyList<PayrollListItemDto>` | `payroll.create` / 40000 |
| `/api/payrolls/{id:guid}/calculate-tax`（新增） | POST | `Payrolls/CalculatePayrollTax` | `PayrollDetailDto` | `payroll.update` / 40171 / 40400 |
| `/api/payrolls` / `{id}`（改造） | POST / PUT | `Payrolls/CreatePayroll` / `UpdatePayroll` | `PayrollDetailDto` | `payroll.create` / `update` / 40000 / 40171 |

- 出参 `PayrollListItemDto` / `PayrollDetailDto` 增 `socialInsurance` / `specialDeduction` / `tax`。

### 3.4 关键用例流程（Handler）

- **CalculatePayrollTax**：取工资单（`40400`）→ `Status == Paid` → `40171`；`GetYearToDateAsync(employeeId, year, month)` → `PayrollTaxCalculator.Calculate(...)` → 写回 `Tax` + 重算 `NetPay`。
- **GeneratePayrolls（改造）**：逐员工：默认社保 / 专项附加 = 0 → 按累计法算 `Tax`（累计数据含本次已生成的此前月份）→ `NetPay`（§0.1）→ 落库。
- **CreatePayroll / UpdatePayroll（改造）**：`Tax` **不接收前端入参**（后端按 §0.2 计算）；`NetPay` 后端计算（`044` 既有语义）。

### 3.5 校验规则（FluentValidation）

| 请求 | 规则 |
|---|---|
| `Create/UpdatePayrollRequest` | 增 `socialInsurance` / `specialDeduction`：≥ 0 且 ≤ `AmountMaxValue`（`tax` **不由前端传入**） |
| `CalculatePayrollTaxRequest` | 无请求体（路径 id） |

## 4. 前端设计

### 4.1 目录（改造 `044` 既有文件）

```
src/
├── api/
│   └── payroll.ts                    # 类型增三字段；新增 calculatePayrollTax
└── views/
    └── HrmManagement/
        ├── PayrollsView.vue          # 列表增「社保 / 个税」列
        └── PayrollFormDrawer.vue     # 增社保 / 专项附加扣除 + 个税（只读）+ 实发预览
```

### 4.2 页面交互

- **`PayrollsView.vue`**：列表增「社保」「个税」列；行操作增「重算个税」（`Draft` 时）。
- **`PayrollFormDrawer.vue`**：增「个人社保」「专项附加扣除」输入（`a-input-number :precision="2"`）；「个税」只读展示（由后端计算，保存后回填）；实发实时预览（`基本 + 津贴 − 社保 − 个税 − 其它扣款`）。
- 已发放（`Paid`）只读（`044` 既有）。

### 4.3 接口层

- `api/payroll.ts`：`PayrollDetail` 增三字段；`calculatePayrollTax(id)`。

## 5. 关键技术决策与取舍

| 决策 | 选择 | 理由 |
|---|---|---|
| 个税用**累计预扣预缴法** | 而非按月单独 | 与居民工资薪金现行预扣方式一致；按月单独会与实际申报不符 |
| 税率表为**代码常量** | 不落库 | 税率变动频率低（政策调整），落库带来配置界面与漂移风险 |
| 社保**人工录入** | 不做方案字典 | 社保基数 / 费率地域差异大，方案字典收益与复杂度不匹配（§5 范围外） |
| `Deduction` 明确为**税后扣款** | 不作税前扣除 | 与"其它扣款"（迟到 / 借支）语义一致 |
| `Tax` **不接受前端入参** | 后端计算 | 防止绕过计算器改税；口径单点 |
| 累计**缺月按 0** | 容错 | 不强制前置月份齐全，避免"必须先补历史"的阻塞 |
| 不接总账 | 无薪酬凭证 | 薪酬与总账联动牵连计提 / 代扣 / 社保三张凭证，另评 |
| 无新增权限点 | 复用 `payroll.*` | 个税计算属薪酬维护，细分收益不足 |

## 6. 单元测试设计（`backend/tests/App.Tests/`）

- **计算器**：各级税率边界（36,000 / 144,000 / …）；减除费用 5000×M；累计已缴税递减；`T ≤ 0` → `Tax = 0`；本期税额不为负。
- **累计**：1..M 累计取值（含缺月按 0）；跨年不累计（仅本年度）。
- **实发公式**：`NetPay` 与 §0.1 一致；边界（社保 / 专项附加 0）。
- **锁定**：`Paid` 重算 `40171`；`Tax` 不由请求体注入。
- **批量生成**：默认社保 / 专项附加 0、个税自动带出；生成后调整重算。
- **字段约束一致性**：新列常量与 EF 列长一致；`044` 用例回归。
