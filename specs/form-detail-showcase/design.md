# 设计规格：表单与详情页样式参照（form-detail-showcase）

> 纯前端参照页面，无后端、无接口。数据为页面内静态 Mock，列表/表单/详情跨路由以 `composables/` 模块级单例共享同一数据源（见 §3.5）。

## 1. 形态决策依据

> 形态选择标准（何时用抽屉 / 独立页 / 弹窗、详情页形态、双形态入口）是工程约定，唯一事实源为前端规则 `.codebuddy/rules/frontend/RULE.mdc` §5.5；本节只记录本参照页面的决策依据。

| 场景 | 依据 |
|---|---|
| 字段少（≤ 8 个）、单屏可容纳 → **抽屉** | 不离开列表上下文，列表滚动位置保留，关闭即回到列表；纵向空间足 |
| 字段多（> 8 个）/ 多分组 / 含子表格 → **独立页面** | 需要完整视口承载分组与子表，且需要独立 URL 可收藏 / 刷新 |
| 弹窗（Modal） | 仅用于**确认类轻交互**（改状态、单字段修改）；本参照不示范业务表单弹窗 |
| 详情页 → **独立页面**（`/xxx/detail/:id`） | 详情只读、信息密度高（可展示创建人 / 更新时间等表单没有的只读字段），与表单职责不同，**不共享组件** |

**详情页与表单页不共享**：详情页用 `a-descriptions` 独立实现；表单用 `a-form`。两者仅共享同一份"数据模型 TS 类型"。

## 2. 数据模型

```ts
/** 订单明细行（子表格） */
interface OrderItemRow {
  key: string
  productName: string
  quantity: number
}

interface OrderRow {
  id: string
  orderNo: string        // 订单号（演示：新增时自动生成）
  customer: string       // 客户
  product: string        // 商品
  amount: number         // 金额
  status: 'pending' | 'paid' | 'shipped' | 'cancelled'
  createdAt: string      // ISO 日期（表单用 a-date-picker）
  remark: string
  createdBy: string      // 仅详情页展示的只读字段（表单无此字段，体现"详情比表单多字段"）
  updatedAt: string      // 仅详情页展示的只读字段
  items: OrderItemRow[]  // 商品明细（每行 1~2 条）
}
```

- 状态选项：`pending 待支付 / paid 已支付 / shipped 已发货 / cancelled 已取消`。
- 状态 Tag 着色：pending `orange`、paid `arcoblue`、shipped `green`、cancelled `gray`。
- 静态种子：约 12 条，`items` 每行 1~2 条明细，状态/金额/时间分散，便于验证筛选与排序。
- 金额展示：`¥ ` 前缀 + `toFixed(2)`。
- 订单号生成：`NO-` + `Date.now()` 后 6 位 + 随机 2 位。

## 3. 统一列表 + 双形态：`/form`

### 3.1 路由

| path | name | component | meta |
|---|---|---|---|
| `/form` | `form` | `() => import('@/views/Showcase/FormShowcaseView.vue')` | `{ requiresAuth: true }` |
| `/form/new` | `formNew` | `() => import('@/views/Showcase/FormPageFormView.vue')` | `{ requiresAuth: true }` |
| `/form/edit/:id` | `formEdit` | 同上（组件内按 `route.name` 区分模式） | `{ requiresAuth: true }` |
| `/form/detail/:id` | `formDetail` | `() => import('@/views/Showcase/FormDetailView.vue')` | `{ requiresAuth: true }` |

均为 `AppLayout` 子路由。侧边菜单：「列表示例」后「表单与详情示例」（`key="form"`，图标 `IconEdit`）。

### 3.2 列表页（`FormShowcaseView.vue`）

参照 `ListShowcaseView` 结构（工具条两行 + 无描边 a-card + a-table）：

- 页面标题：「订单列表（表单与详情示例）」。
- 筛选行：关键词（搜索订单号/客户）+ 状态下拉 + 搜索/重置按钮，`draft/applied` 分离，`tableKey` 重挂载回第 1 页。
- 操作行：「新增·抽屉」（`a-button type=primary size=small`，`<IconPlus />`）+ 竖 `a-divider` +「新增·页面」（`a-button size=small`，`<IconPlus />`）。
- 表格列：序号、订单号、客户、商品、金额（`¥` 格式化）、状态（`a-tag`）、创建时间（`YYYY-MM-DD`）、操作列。
- 操作列（均 `a-button type=text size=small`）：
  - `查看` → `router.push({ name: 'formDetail', params: { id } })`。
  - `编辑` → `a-dropdown` 形态菜单（trigger click）：`用抽屉`（开抽屉表单，回填该行）/ `用页面`（`router.push({ name: 'formEdit', params: { id } })`）。
  - `删除` → `a-popconfirm` 确认后 `remove(id)`。
- 数据源：`useOrderData()` 模块级单例（见 3.5）。

### 3.3 抽屉表单（`Showcase/OrderFormDrawer.vue`，新增/编辑共用）

- Props：`visible: boolean`；`mode: 'create' | 'edit'`；`editId?: string`（编辑时的行 id）。
- Emits：`update:visible`、`saved`（提交成功后通知列表）。
- 结构：`a-drawer`（`:width="560"`，`unmount-on-close`，`:footer="false"` 关闭内置底栏，表单底部自定义操作栏 提交/取消；提交手动 `formRef.validate()`，失败保持打开、成功写入数据源并关闭。不用内置 `ok-text`/`before-ok` 机制，与独立表单页交互统一、行为可控）。
- 表单字段（`a-form :model="form" :rules="rules"`，`layout="vertical"`，单列）：
  | 字段 | 控件 | 校验 |
  |---|---|---|
  | 订单号 `orderNo` | `a-input`（新增时 `placeholder="保存时自动生成"`，新增态只读） | 新增时允许空；编辑时必填 |
  | 客户 `customer` | `a-input` | 必填 |
  | 商品 `product` | `a-input` | 必填 |
  | 金额 `amount` | `a-input-number :min="0" :precision="2" style="width:100%"` | 必填且 > 0（`positive` 规则） |
  | 状态 `status` | `a-select`（默认 `pending`） | 必填 |
  | 创建时间 `createdAt` | `a-date-picker style="width:100%"`（存 `YYYY-MM-DD` 字符串） | 必填 |
  | 备注 `remark` | `a-textarea :auto-size="{ minRows: 2, maxRows: 4 }"` | 无 |
- 打开时按 mode 初始化：create 全空（status 默认 pending）；edit 从数据源深拷贝回填（不污染数据源）。
- 提交：`upsert` 时保留编辑行原 `items`（深拷贝）；新增时 `items` 取主商品 1 条（`{ key, productName: product, quantity: 1 }`），保证统一详情页子表格非空。

### 3.4 独立表单页（`FormPageFormView.vue`，新增/编辑共用）

- 模式判断：`route.name === 'formEdit'` 且 `params.id` 存在 → 编辑模式，从数据源回填；否则新增。
- 布局：`a-page-header`（title 新增订单/编辑订单，`@back` 回列表）+ `a-card` 内 `a-form`（`layout="vertical"`）：
  - **分组 1「基本信息」**（`a-row :gutter="24"` + `a-col :span="12"` 两列）：订单号（新增只读占位）、客户*、商品*、金额*（`a-input-number`）、状态*（默认 pending）、创建时间*（`a-date-picker`）。
  - **分组 2「商品明细」**：`a-table`（`:data="form.items"`，`:pagination="false"`，row-key="key"，size="small"）列：序号、商品名称（`a-input` 绑 `record.productName`）、数量（`a-input-number :min="1"` 绑 `record.quantity`）、操作（删除行 `a-button type=text status=danger`）；表格上方右侧「添加行」按钮。
  - **分组 3「其他」**：备注（`a-textarea`）。
  - 分组间用 `a-divider`（`orientation="left"`，文字为分组名）分隔。
- 底部操作栏：`.form-footer`（`border-top`，右对齐）：「提交」（`type=primary`）+「取消」（回列表）。
- 校验（`:rules`）：customer/product 必填；amount 必填且 > 0；createdAt 必填；`items` 手动校验：至少 1 行且每行 `productName.trim()` 非空、`quantity >= 1`，错误提示「请至少添加一条商品明细并填写商品名称」。
- 提交：校验失败 → 页面不动（`Message.error('请检查表单填写')`）；成功 → `upsert` → `Message.success` → `router.push({ name: 'formDetail', params: { id } })`。

### 3.5 共享数据源

- 文件：`src/composables/useOrderStore.ts`，导出 `useOrderData()`（模块级单例 `ref<OrderRow[]>` + `findById(id)` + `upsert(row)` + `remove(id)` + `reset()`）。
- 理由：路由切换会销毁当前页实例，抽屉/表单/详情需跨页面保持会话内数据 → 用 composable 模块级 ref 维持共享（**纯前端演示，非 Pinia**，避免为一个示例引入全局状态）。
- 列表页、表单页、详情页、抽屉均通过 `useOrderData()` 操作同一数据源。

### 3.6 统一详情页（`FormDetailView.vue`）

- 页面头：`a-page-header`（`title="订单详情"`，subtitle 订单号，`@back` 回列表）+ 右侧操作「编辑」按钮（打开抽屉表单，回填该行）+「删除」（Popconfirm，删除后回列表）。
- 主体：`a-card` 内 `a-descriptions`（`:column="2"`，bordered）：订单号、客户、商品、金额（`¥`）、状态（`a-tag`）、创建时间、备注（`-` 兜底）、创建人、更新时间（**表单没有的只读字段，体现详情与表单不共享**）。
- 明细子表格：`a-divider orientation="left"`「商品明细」+ `a-table`（序号/商品名称/数量，`:pagination="false"`）。
- 未知 id：`a-result status="404" subtitle="订单不存在或已被删除"` + 「返回列表」按钮。

## 4. 交互与状态细节（通用）

- 删除：列表页/详情页 Popconfirm 确认后 `remove(id)`；详情页删除后 `router.push` 回列表。
- 提交后时间：`updatedAt` = 当天日期（`upsert` 内统一处理）。
- 创建人：种子数据为 `admin`；新增数据为 `admin`（演示态固定）。
- 列表条件变化回第 1 页：沿用 `tableKey` 重挂载模式（前端规则 §5「交互模式」）。
- 所有表单校验文案使用中文。

## 5. 技术决策

| 决策 | 理由 |
|---|---|
| 详情与表单不共享组件，仅共享数据模型类型 | 职责不同（只读密度 vs 录入校验）；共享组件会导致 `v-if` 态爆炸；此为本规格要沉淀的核心约定 |
| 两个平行示例（例 A /form-drawer、例 B /form-page）合并为单一列表 `/form` | 两列表页同源、同列、同筛选、同分页，仅操作列跳转不同，属重复演示；合并后「一套列表 + 双形态入口」更贴近真实使用方式，参照密度更高 |
| 新增用两个按钮、编辑用单按钮 + 形态菜单 | 新增是工具条主操作区，空间足，双入口一眼可见；操作列在表格内，空间紧，单按钮 + dropdown 菜单保持紧凑 |
| 只保留一个统一详情页（含明细子表格） | 两详情仅差一个子表，数据源每行本就有 `items`，合并后字段最全且不丢信息；抽屉编辑在详情页直接复用（体现"详情页也可开抽屉编辑"） |
| 表单/详情/列表用模块级 composable 单例共享数据 | 独立路由切换会销毁列表页实例，会话内数据需跨页面保持；用 `composables/` 模块级 ref 最小化实现，不引入 Pinia |
| 不实现表单-详情组件复用 | 需求 3.2 明确为反例边界；后续真实业务可按字段量自行取舍，以规则约定为准 |

## 6. 文件清单

| 文件 | 操作 |
|---|---|
| `frontend/src/composables/useOrderStore.ts` | 保留（注释按新结构微调） |
| `frontend/src/views/Showcase/FormShowcaseView.vue` | 新增（统一列表，双新增按钮 + 编辑形态菜单） |
| `frontend/src/views/Showcase/OrderFormDrawer.vue` | 迁移（自 `FormDrawerShowcase/components/`，行为不变） |
| `frontend/src/views/Showcase/FormPageFormView.vue` | 保留（路由名 `formPageNew/formPageEdit` → `formNew/formEdit`，跳转目标改 `formDetail`） |
| `frontend/src/views/Showcase/FormDetailView.vue` | 新增（统一详情，合并两详情 + 明细子表格，编辑走抽屉） |
| `frontend/src/views/FormDrawerShowcaseView.vue` | 删除 |
| `frontend/src/views/FormDrawerDetailView.vue` | 删除 |
| `frontend/src/views/FormPageShowcaseView.vue` | 删除 |
| `frontend/src/views/FormPageDetailView.vue` | 删除 |
| `frontend/src/views/FormDrawerShowcase/` | 删除（OrderFormDrawer 迁移后清目录） |
| `frontend/src/router/index.ts` | 修改（6 条路由替换为 4 条：`/form`、`/form/new`、`/form/edit/:id`、`/form/detail/:id`） |
| `frontend/src/components/AppLayout.vue` | 修改（菜单项 `key="formDrawer"` → `key="form"`） |
| `frontend/e2e/form-showcase.spec.ts` | 新增（合并两个旧 spec） |
| `frontend/e2e/form-drawer-showcase.spec.ts` | 删除 |
| `frontend/e2e/form-page-showcase.spec.ts` | 删除 |

## 7. e2e 设计

前置：登录（复用 `list-showcase.spec.ts` 的 `login`/凭据模式）。

### `form-showcase.spec.ts`

1. 菜单「表单与详情示例」进入 `/form`，标题「订单列表（表单与详情示例）」可见，表格 10 行，总数 `共 12 条`。
2. 抽屉新增：「新增·抽屉」→ 抽屉可见（`.arco-drawer` + 标题「新增订单」）；空表单提交 → 抽屉不关闭且出现「请输入客户」；填必填项提交 → 抽屉关闭、成功提示、新行可见、总数 `共 13 条`。
3. 抽屉编辑：行「编辑」→ dropdown「用抽屉」→ 抽屉标题「编辑订单」且客户回填；改金额提交 → 列表该行金额更新。
4. 页面新增：「新增·页面」→ URL `/form/new`；子表格默认 1 空行；「添加行」→ 2 行；空表单提交 → URL 不变且「请输入客户」「请至少添加一条商品明细并填写商品名称」可见；填必填 + 明细提交 → URL `/form/detail/<id>`，详情显示所填客户与明细商品名。
5. 页面编辑：行「编辑」→ dropdown「用页面」→ URL `/form/edit/:id`，客户回填；提交 → 回详情页且数据更新。
6. 详情：行「查看」→ URL `/form/detail/`，`a-descriptions` 显示该行订单号、创建人/更新时间与商品明细子表格；点「编辑」→ 抽屉打开且回填。
7. 直接访问 `/form/detail/not-exist` → 404 文案「订单不存在或已被删除」与「返回列表」按钮。
8. 删除：行「删除」Popconfirm 确认后总数 -1。

> 选择器优先 `getByRole`/`getByLabel`/`getByPlaceholder`；Arco 动态元素用稳定类名（`.arco-drawer`、`.arco-drawer-title`、`.arco-result`）兜底。形态菜单选项渲染为 `li.arco-dropdown-option`（无 ARIA 角色），用 `page.locator('.arco-dropdown-option', { hasText: '用抽屉' })` 定位。
