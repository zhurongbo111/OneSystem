# 任务清单：列表操作列约定（action-column）

## 任务

- [x] T1 前端规则 `.codebuddy/rules/frontend/RULE.mdc` 新增 §5.2「操作列约定」（数量阈值 / 颜色 / 图标 / 呈现），并在 §5 表格配置行加指引
- [x] T2 `ListShowcaseView.vue` 操作列改造：4 演示操作（编辑 / 详情 / 删除 平铺 + 更多收纳重置密码），图标、颜色、popconfirm 对齐约定
- [x] T3 `UsersView.vue` 操作列改造：编辑 / 详情 / 禁用 平铺（图标 + 颜色），重置密码收纳进「更多」；操作列 `width` 260 → 220；既有行为（抽屉 / 详情跳转 / 启停 popconfirm / 行内 loading / 重置模态）不变
- [x] T4 e2e `list-showcase.spec.ts` 补操作列用例（平铺按钮、更多下拉、危险样式、删除链路回归）
- [x] T5 验证：`npm run build` + `npm run lint` + `npm run test:e2e`（含 `user-management.spec.ts` 回归）全通过
- [x] T6 列宽策略落地（design.md §2.1）：所有列固定 `width`、邮箱列 `ellipsis`、表格 `:scroll="{ x: tableScrollX }"`、操作列 240 + `action-cell` nowrap 兜底（`UsersView.vue` / `ListShowcaseView.vue`）
- [x] T7 e2e 补「表头与内容对齐」断言（操作列 th 宽 ≥ 按钮组实际宽度），防回退
- [x] T8 前端规则 §5 补「列宽策略」条款

## 变更：单据列表操作列顺序与配色（2026-09-16）

- [x] T9 单据列表（采购 / 销售）操作列顺序调整为 详情 → 结算切换 → 作废（§5.1 主操作 → 中性 → 完成 → 警示 → 危险）
- [x] T10 单据「作废」由 warning 升为 `status="danger"`、「已作废」标签由灰改红；结算文案统一为「未结算 / 已结算」（标签、按钮、popconfirm、操作提示、筛选下拉），详情页对齐
- [x] T11 e2e `purchase.spec.ts` / `sale.spec.ts` 同步文案断言，并补操作列顺序与 danger 配色断言
- [x] T12 验证：`npm run build` + `npm run lint` + `npm run test:e2e` 全通过

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过。
