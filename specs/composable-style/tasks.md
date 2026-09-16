# 任务清单：组合式 API 分区书写规范

## 规格与规则

- [x] 1. 在 `.codebuddy/rules/frontend/RULE.mdc` 新增「组合式 API 分区书写规范」章节（分区顺序表 + 模板 ref 命名 + composable 顺序）

## ESLint 强制

- [x] 2. 安装 `eslint-plugin-perfectionist`（dev 依赖）
- [x] 3. `eslint.config.js` 配置：`perfectionist/sort-imports`（vue 生态 → 第三方 → `@/` 内部，组间空行）
- [x] 4. 验证 `<script setup>` 顶层声明顺序规则可行性：perfectionist v4 无对应规则 → 采用降级方案（import 顺序 lint 强制，分区顺序靠规则文档 + review），已记入 design.md §6 与 requirement.md

## 存量对齐

- [x] 5. 重构 `views/Showcase/ListShowcaseView.vue` script 分区
- [x] 6. 重构 `views/Showcase/FormShowcaseView.vue` script 分区
- [x] 7. 重构 `views/Showcase/FormPageFormView.vue` script 分区（`editId`/`isEdit` 由 computed 改常量，同步修正引用）
- [x] 8. 重构 `views/Showcase/FormDetailView.vue` script 分区
- [x] 9. 重构 `views/Showcase/OrderFormDrawer.vue` script 分区
- [x] 10. 重构 `views/LoginView.vue` / `HomeView.vue` / `AppLayout.vue` / `App.vue` / `ComponentShowcaseView.vue`
- [x] 11. 重构 `composables/useOrderStore.ts` 分区（types → constants → helpers → hook body）

## 验证

- [x] 12. `npm run lint` 通过；乱序 import 触发 `perfectionist/sort-imports` 报错（临时文件验证后删除）
- [x] 13. `npm run type-check` 通过
- [x] 14. 前后端 dev 已运行，`npm run test:e2e` 30/30 通过
