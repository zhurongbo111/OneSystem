# 任务清单：列表操作列约定（action-column）

## 任务

- [x] T1 前端规则 `.codebuddy/rules/frontend/RULE.mdc` 新增 §5.2「操作列约定」（数量阈值 / 颜色 / 图标 / 呈现），并在 §5 表格配置行加指引
- [x] T2 `ListShowcaseView.vue` 操作列改造：4 演示操作（编辑 / 详情 / 删除 平铺 + 更多收纳重置密码），图标、颜色、popconfirm 对齐约定
- [x] T3 `UsersView.vue` 操作列改造：编辑 / 详情 / 禁用 平铺（图标 + 颜色），重置密码收纳进「更多」；操作列 `width` 260 → 220；既有行为（抽屉 / 详情跳转 / 启停 popconfirm / 行内 loading / 重置模态）不变
- [x] T4 e2e `list-showcase.spec.ts` 补操作列用例（平铺按钮、更多下拉、危险样式、删除链路回归）
- [x] T5 验证：`npm run build` + `npm run lint` + `npm run test:e2e`（含 `user-management.spec.ts` 回归）全通过

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过。
