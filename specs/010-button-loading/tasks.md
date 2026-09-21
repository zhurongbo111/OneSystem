---
created: 2026-09-10
updated: 2026-09-16
---

# 任务清单：按钮交互反馈（loading）

## 规格与规则

- [x] 1. 新建 `specs/010-button-loading/`（requirement / design / tasks）
- [x] 2. `.codebuddy/rules/frontend/RULE.mdc` 新增 §4.6「按钮交互反馈（强制）」；§5 交互模式补查询并发与请求序号约定

## 前端改造

- [x] 3. `views/LoginLogManagement/LoginLogsView.vue`：搜索 / 重置 / 刷新按钮绑定查询 `loading`
- [x] 4. `views/UserManagement/UsersView.vue`：搜索 / 重置 / 刷新按钮绑定查询 `loading`
- [x] 5. `views/UserManagement/UsersView.vue`：新增 `togglingId`，行内启用 / 禁用按钮绑定并按 §4.6 防重入
- [x] 6. 核对表单提交类文件（`UserFormDrawer` / `FormPageFormView` / `OrderFormDrawer` / `LoginView`）已符合 §4.6，无需改动
- [x] 7. 核对纯同步页面（`ListShowcaseView` / `FormShowcaseView` / `FormDetailView` / `UserDetailView` / `ComponentShowcaseView`）按规则不置 loading，保持不变

## e2e 覆盖

- [x] 8. `e2e/user.spec.ts`：搜索 / 刷新按钮 loading 出现并恢复
- [x] 9. `e2e/user.spec.ts`：行内禁用按钮 loading 出现并恢复
- [x] 10. `e2e/login-log.spec.ts`：搜索按钮 loading 出现并恢复

## 验证

- [x] 11. `npm run lint` 通过
- [x] 12. `npm run type-check` 通过
- [x] 13. `npm run test:e2e` 全量通过（46 通过，含新增 3 条 loading 用例与 1 条抽屉回填竞态回归用例）
