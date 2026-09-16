---
created: 2026-09-15
updated: 2026-09-16
---

# 任务清单：图标示例（icon-showcase）

## 前端

- [x] F1 `frontend` 安装 `@tabler/icons-vue`（`npm install @tabler/icons-vue`）
- [x] F2 `ComponentShowcaseView.vue` 新增「图标」tab（`key="icon"`）：Tabler 常用图标网格、尺寸/线宽、颜色、组合用法四个 `a-card` 示例块
- [x] F3 新增 e2e 用例：`component-showcase.spec.ts` 切到「图标」tab 断言内容可见
- [x] F4 `npm run type-check` / `npm run build` 通过；`npm run test:e2e` 通过
- [x] F5 `frontend` 安装 `@lucide/vue`（`npm install @lucide/vue`）
- [x] F6 `ComponentShowcaseView.vue` 图标 tab 新增 Lucide 段（分组标题分隔）：Lucide 常用图标网格、尺寸/线宽/颜色、按钮/标签组合用法示例块
- [x] F7 扩展 e2e 用例：图标 tab 用例同时断言 Tabler 与 Lucide 示例内容可见
- [x] F8 `npm run type-check` / `npm run build` 通过；`npm run test:e2e` 通过
- [x] F9 `ComponentShowcaseView.vue` 图标 tab 新增 Arco 段（段首）：Arco 常用图标网格、尺寸/线宽/颜色（CSS color）、rotate/spin、按钮/标签/菜单组合用法示例块
- [x] F10 Tabler / Lucide 段分组标题补充图标库来源（`Tabler Icons（@tabler/icons-vue）`）
- [x] F11 扩展 e2e 用例：图标 tab 用例同时断言 Arco / Tabler / Lucide 三段示例内容可见（重名图标改断言各段独有文本）
- [x] F12 `npm run type-check` / `npm run build` 通过；`npm run test:e2e` 通过
