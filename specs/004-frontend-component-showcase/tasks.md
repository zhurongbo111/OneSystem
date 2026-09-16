---
created: 2026-09-10
updated: 2026-09-16
---

# 任务清单：前端组件示例页面（frontend-component-showcase）

## 前端

- [x] F1 新建 `src/views/Showcase/ComponentShowcaseView.vue`：`a-tabs` 五分类（基础/表单/数据展示/反馈/导航）+ 各分类 `a-card` 组件示例，演示数据 TS 显式类型
- [x] F2 `src/router/index.ts` 注册 `/components`（`name=components`，`public`，懒加载）
- [x] F3 `src/views/HomeView.vue` 头部新增"组件示例"导航按钮，跳转 `/components`
- [x] F4 新增 e2e 用例 `frontend/e2e/component-showcase.spec.ts`：`/components` 渲染、tab 切换、首页导航跳转
- [x] F5 `npm run build` 通过；`npm run test:e2e` 通过
