# 设计规格：前端组件示例页面（frontend-component-showcase）

## 1. 总体设计

在现有前端骨架上新增一个纯前端展示页面：不引入新依赖、不改动接口层（`src/api/`）与后端。仅新增 1 个视图、1 条路由、首页 1 个导航入口与 1 份 e2e 用例。

## 2. 路由设计

- 新增路由：`path: '/components'`，`name: 'components'`，`component: () => import('@/views/Showcase/ComponentShowcaseView.vue')`，`meta: { public: true }`。
- 懒加载（`() => import(...)`，前端规则 §5）。
- `public`：无需登录即可访问（示例 / 参考性质，便于直接访问与 e2e 前置最小）。
- 在 `src/router/index.ts` 的 `routes` 数组注册，位置在兜底 `/:pathMatch(.*)*` 重定向之前。

## 3. 页面设计（`src/views/Showcase/ComponentShowcaseView.vue`）

- 结构：
  - 顶部标题栏：页面标题"Arco Design 组件示例" + "返回首页"入口（`router.push({ name: 'home' })`，未登录会经守卫跳登录页，属预期行为）。
  - 主体 `a-tabs`：5 个 tab —— 基础 / 表单 / 数据展示 / 反馈 / 导航，`v-model:activeKey` 受控切换。
  - 每个 tab 内用多个 `a-card`（`title` 为组件名 / 分类名）分块展示组件。
- 数据：组件所需静态演示数据（表格数据、下拉选项、级联选项、树数据、时间线等）以本地 `ref` / `const` 定义，类型用 TS 显式声明（禁止 `any`，前端规则 §6）。
- 少量交互示例：
  - Switch / Checkbox / Radio / Slider / Rate：用 `ref` 绑定并展示当前值。
  - Modal / Drawer：按钮触发（`ref` 控制 `visible`）。
  - Notification / Message：按钮触发（`import { Message, Notification } from '@arco-design/web-vue'`）。
  - Table：基础分页。
- 样式：仅 Arco 组件 + `scoped` 少量布局样式 + Arco CSS 变量；不自造样式体系（前端规则 §4）。

## 4. 导航入口（`src/views/HomeView.vue`）

- 头部 `header-right` 区域新增"组件示例" `a-button`（`type="text"`），`@click` 执行 `router.push({ name: 'components' })`。
- 不改变现有登录 / 退出逻辑。

## 5. 组件清单（页面展示）

| 分类（tab） | 组件 |
|---|---|
| 基础 | Button、Typography |
| 表单 | Input、Select、Checkbox、Radio、Switch、Slider、Rate、DatePicker、Cascader、Upload |
| 数据展示 | Table、Tag、Badge、Avatar、Descriptions、Timeline、Statistic、Progress、Tree |
| 反馈 | Alert、Result、Spin、Skeleton、Modal、Drawer、Tooltip、Popover、Notification |
| 导航 | Menu、Tabs、Breadcrumb、Steps、Pagination、Dropdown |

## 6. 技术决策

| 决策 | 理由 |
|---|---|
| 页面设为 `public`（无需登录） | 示例 / 参考性质；便于直接访问与 e2e 测试，无登录前置 |
| 用 `a-tabs` 分类组织 | 一屏聚焦一个分类，避免超长滚动；同时展示 Tabs 组件本身 |
| 演示数据本地 `ref` / `const` 声明 | 纯前端示例，无需接口；TS 显式类型保证可读与类型安全 |
| 不改接口层 / 后端 | 与后端无关，范围最小化，降低回归风险 |

## 7. e2e 设计（`frontend/e2e/component-showcase.spec.ts`）

- 依赖：前端 dev（5173）；页面为 `public`，不依赖后端接口，故本 spec 无需 `beforeAll` 检查后端健康。
- 用例：
  1. 直接访问 `/components` 正常渲染（页面标题可见，默认"基础"tab 内容可见）。
  2. 切换 tab（如切到"表单"），对应分类内容可见。
  3. 已登录态下从首页点击"组件示例"跳转到 `/components`（先走登录流程，再点导航入口）。
