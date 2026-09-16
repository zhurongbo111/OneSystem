---
created: 2026-09-10
updated: 2026-09-16
---

# 需求规格：前端组件示例页面（frontend-component-showcase）

## 1. 背景

项目脚手架已建立前端骨架（登录页 + 首页）。为便于开发者快速了解项目采用的 Arco Design Vue 组件库的用法与视觉风格，需要一个组件示例（showcase）页面，集中展示常用组件的基本用法，作为 UI 参考。

## 2. 目标

1. 提供一个前端组件示例页面，按分类集中展示 Arco Design Vue 常用组件。
2. 页面通过路由独立可访问，且可从首页导航进入。
3. 页面内组件示例为标准用法，可直接作为开发参考。

## 3. 功能点

- F1 组件示例页：`src/views/Showcase/ComponentShowcaseView.vue`，按分类（基础 / 表单 / 数据展示 / 反馈 / 导航）分组展示 Arco Design 组件。
- F2 路由：`/components` 独立可访问（`public`，无需登录）。
- F3 导航入口：首页头部新增"组件示例"入口，可跳转组件示例页。
- F4 覆盖组件（示例，非穷举）：
  - 基础：Button、Typography
  - 表单：Input、Select、Checkbox、Radio、Switch、Slider、Rate、DatePicker、Cascader、Upload
  - 数据展示：Table、Tag、Badge、Avatar、Descriptions、Timeline、Statistic、Progress、Tree
  - 反馈：Alert、Result、Spin、Skeleton、Modal、Drawer、Tooltip、Popover、Notification
  - 导航：Menu、Tabs、Breadcrumb、Steps、Pagination、Dropdown
- F5 页面使用 Arco 组件 + Arco 主题变量分块展示，不自造样式体系（前端规则 §4）。

## 4. 验收标准

1. `cd frontend && npm run build` 通过，无 TypeScript 错误。
2. 访问 `/components` 正常渲染，各分类组件均可见。
3. 首页点击"组件示例"可跳转 `/components`。
4. e2e 覆盖（`npm run test:e2e` 通过）：`/components` 页面加载渲染、分类 tab 切换、首页导航入口跳转。

## 5. 范围外（不做）

- 不展示全部 Arco 组件（仅常用子集，后续按需补充）。
- 不做组件交互状态管理的完整示例（仅基础用法 + 少量交互）。
- 不涉及后端接口（纯前端静态示例）。
