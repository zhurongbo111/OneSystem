# 需求规格：图标示例（icon-showcase）

## 1. 背景

前端组件示例页（`frontend-component-showcase`）集中展示各图标库的用法，作为业务页面选图标的参考。涉及三套图标库：Tabler Icons（`@tabler/icons-vue`，业务默认选用）、Lucide Icons（`@lucide/vue`，回退补充）与 Arco Design 自带图标（`@arco-design/web-vue` 的 `Icon*`，兜底；三套均为线性风格）。三者在示例页「图标」tab 内分组展示，供对照选用。选型优先级与落地约定见前端规则 §4.7。

## 2. 目标

1. 安装 Tabler 图标 Vue 封装包 `@tabler/icons-vue` 与 Lucide 图标 Vue 封装包 `@lucide/vue`（Arco 图标为 `@arco-design/web-vue` 自带，无需额外安装）。
2. 在组件示例页（`/components`）新增「图标」分类，集中展示 Arco、Tabler、Lucide 三套图标的基本用法，可直接作为开发参考。
3. 三套图标并存、互不影响（业务代码按需选用）。

## 3. 功能点

- F1 依赖：`frontend` 安装 `@tabler/icons-vue`、`@lucide/vue`。
- F2 图标示例 tab：`src/views/Showcase/ComponentShowcaseView.vue` 的 `a-tabs` 新增第 6 个 tab「图标」（`key="icon"`），用 `a-card` 分块展示 Arco、Tabler、Lucide 三套图标用法（三套以分组标题分隔，顺序：Arco → Tabler → Lucide）。
- F3 覆盖示例（非穷举），每套图标库各覆盖：
  - 基础用法：若干常用图标图标网格，标注图标组件名（Arco 如 `IconHome`，Tabler 如 `IconHome`，Lucide 如 `House` / `Search`）。
  - 尺寸：同一图标不同 `size` 对比。
  - 线宽：不同线宽对比（Arco / Tabler / Lucide 均为 `stroke-width`，Arco 默认 4）。
  - 颜色：不同颜色的图标（Arco 图标无 `color` prop，经 CSS `color` / `currentColor` 着色；Tabler / Lucide 用 `color`）。
  - 组合用法：图标 + 文字按钮（`a-button` 内嵌图标）、图标 + 标签（`a-tag`）、图标在 `a-menu-item` 中。
  - Arco 特有：`rotate`（旋转）与 `spin`（旋转动画）演示。

## 4. 验收标准

1. `cd frontend && npm run build` 通过，无 TypeScript 错误。
2. 访问 `/components`，切换到「图标」tab，Arco、Tabler、Lucide 各示例块均可见，图标正常渲染。
3. e2e 覆盖（`npm run test:e2e` 通过）：切到「图标」tab 后三套图标示例内容均可见。

## 5. 范围外（不做）

- 不展示全部图标（每套仅常用子集）。
- 不改动既有页面的 Arco 图标用法。
- 不涉及后端接口（纯前端静态示例）。
