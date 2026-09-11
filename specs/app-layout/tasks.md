# 任务清单：全局页面布局（app-layout）

## 规格

- [x] S1 编写 `requirement.md` / `design.md` / `tasks.md`（本文件）

## 前端

- [x] F1 新建 `src/components/AppLayout.vue`：`a-layout`（has-sider）三栏，`a-layout-sider` 可折叠 + `a-menu`（inline，`selected-keys` 绑定 `route.name`，点击 `router.push`），`a-layout-header` 折叠按钮 + logo + 用户下拉（`a-dropdown`，含退出登录），`a-layout-content` 内嵌 `RouterView`；`onMounted` 调 `fetchCurrentUser`
- [x] F2 `src/router/index.ts` 重组为嵌套路由：新增 `layout` 父路由（`AppLayout`），`home`（`path:''`，requiresAuth）与 `components`（public）改为子路由；`/login` 独立不变；守卫逻辑不变
- [x] F3 `src/views/HomeView.vue` 移除自带头部（`a-layout`/header/用户与按钮）、`onMounted` 的 `fetchCurrentUser`、`onLogout`、`goComponents`，仅保留欢迎卡片与用户 descriptions
- [x] F4 `src/views/ComponentShowcaseView.vue` 移除顶部"返回首页"按钮，保留页面标题与 tabs 主体
- [x] F5 新增 e2e `frontend/e2e/app-layout.spec.ts`：布局渲染与菜单选中联动、菜单切换、侧边栏折叠/展开、用户下拉退出登录
- [x] F6 调整既有 e2e：`login.spec.ts` 退出登录改为经用户下拉；`component-showcase.spec.ts` "组件示例"入口改为侧边菜单
- [x] F7 `npm run build` 通过、`npm run lint` 通过；前后端 dev 启动后 `npm run test:e2e` 全通过（10 用例）

## 变更：示例页面菜单分组

- [x] G1 `specs/app-layout/requirement.md` / `design.md` 更新：侧边菜单「组件示例 / 列表示例 / 表单与详情示例」收纳进「示例页面」`a-sub-menu`（默认展开，`open-keys` 与 `@open-change` 联动）
- [x] G2 `AppLayout.vue` 侧边菜单改为：首页 + 示例页面（子菜单：组件示例 / 列表示例 / 表单与详情示例）；子菜单默认展开
- [x] G3 `npm run build` + `npm run lint` + `npm run test:e2e` 全通过（50 用例）
