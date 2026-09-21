---
created: 2026-09-10
updated: 2026-09-16
---

# 任务清单：全局页面布局（app-layout）

## 规格

- [x] S1 编写 / 更新 `requirement.md` / `design.md` / `tasks.md`（含侧边菜单分组与子菜单折叠约定）

## 前端

- [x] F1 新建 `src/components/AppLayout.vue`：`a-layout`（has-sider）三栏，`a-layout-sider` 可折叠 + `a-menu`（inline，`selected-keys` 绑定 `route.name`，点击 `router.push`；「首页」+「示例页面」`a-sub-menu`：组件示例 / 列表示例 / 表单与详情示例；子菜单**默认折叠**，仅当前路由所属分组自动展开——`watch(route.name, { immediate: true })` 只增不减），`a-layout-header` 折叠按钮 + logo + 用户下拉（`a-dropdown`，含退出登录），`a-layout-content` 内嵌 `RouterView`；`onMounted` 调 `fetchCurrentUser`
- [x] F2 `src/router/index.ts` 重组为嵌套路由：新增 `layout` 父路由（`AppLayout`），`home`（`path:''`，requiresAuth）与 `components`（public）改为子路由；`/login` 独立不变；守卫逻辑不变
- [x] F3 `src/views/HomeView.vue` 移除自带头部（`a-layout`/header/用户与按钮）、`onMounted` 的 `fetchCurrentUser`、`onLogout`、`goComponents`，仅保留欢迎卡片与用户 descriptions
- [x] F4 `src/views/Showcase/ComponentShowcaseView.vue` 移除顶部"返回首页"按钮，保留页面标题与 tabs 主体
- [x] F5 新增 e2e `frontend/e2e/app-layout.spec.ts`：布局渲染与菜单选中联动、菜单切换、侧边栏折叠/展开、用户下拉退出登录；首页子菜单默认折叠、点击分组标题展开、直接访问子页面时所属分组自动展开
- [x] F6 调整既有 e2e：`login.spec.ts` 退出登录改为经用户下拉；`component-showcase.spec.ts` "组件示例"入口改为侧边菜单
- [x] F7 新增 e2e 公共 helper `frontend/e2e/helpers/menu.ts`（`clickMenuItem`：子项不可见时先展开所属分组），改造各 spec 中点击子菜单项处
- [x] F8 验证：`npm run build` 通过、`npm run lint` 通过；前后端 dev 启动后 `npm run test:e2e` 全通过
