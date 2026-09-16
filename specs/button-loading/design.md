# 设计规格：按钮交互反馈（loading）

> 遵循 `AGENTS.md`（测试门槛 §6）与前端规则。
> **约定正文的唯一事实源是本规格 §0**（前端规则 `.codebuddy/rules/frontend/RULE.mdc` §4.6 只留判据与指针）；§1 起记录决策理由、存量改造点、e2e 与风险。

## 0. 约定正文（唯一事实源）

**绑定粒度：loading 属于「操作」而非「按钮」。** 同一个异步操作无论有几个触发入口（按钮 / 输入框回车 / 分页器 / 行内按钮），共用同一个 loading 状态；不同操作各用独立状态，**禁止多个按钮共用一个 `loading`**。

| 操作类型 | 状态命名 | 绑定方式 |
|---|---|---|
| 列表查询（搜索 / 重置 / 刷新 / 翻页 / 每页条数） | `loading` | 按钮 + 回车 `:loading="loading"`，同时 `<a-table :loading="loading">` |
| 表单提交（新增 / 编辑） | `submitting` | 提交按钮 `:loading="submitting"` |
| 弹窗确认（重置密码 / 改状态等） | `xxSubmitting` | `a-modal :ok-loading="xxSubmitting"` |
| 行内操作（启用 / 禁用 / 单行删除） | `xxingId`（如 `togglingId`） | `:loading="togglingId === row.id"` |
| 批量操作（批量删除 / 批量改状态） | `batchXxing`（如 `batchDeleting`） | 触发按钮 `:loading="batchDeleting"`，与 `:disabled="!selectedIds.length"` 叠加 |

**实现要求**

- **触发即置位**：handler 内先 `xxLoading.value = true` 再 `await`；用 `try/finally` 复位，成功与失败路径都要复位。
- **防重入**：动作类（提交 / 删除 / 启停）handler 开头 `if (xxLoading.value) return`——Arco 的 `loading` 只表现为不可点击，**不阻止事件重入**（回车 + 点击、弹窗确认连点）。
- **查询类不丢弃点击**：搜索 / 翻页等查询动作不加判空，最新意图必须生效；用请求序号仲裁乱序响应（见 `specs/list-showcase/design.md` §0「交互模式」）。
- **不置 loading 的情形**：同步瞬时动作（打开抽屉 / 弹窗、路由跳转、本地导出 CSV、复制文本）不置；只有「发起请求并等待结果」才置。
- **与业务 `:disabled` 叠加**：如 `:disabled="!selectedIds.length"` + `:loading="loading"`；`a-button` 的 `loading` 已隐含不可点击，无需为它另写 `:disabled`。
- **e2e**：按钮 loading 属用户可感知行为，相关功能交付前须有 e2e 断言（`AGENTS.md` §6）。

## 1. 核心决策与理由

**loading 属于「操作」，不属于「按钮」。** 同一异步操作无论有几个触发入口（按钮点击、输入框回车、分页器、行内操作）都共用同一个状态；不同操作各用独立状态。命名与绑定方式见本规格 §0 表。

- **为什么查询类与表格共用 `loading`**：两者描述的是同一个事实（"正在查询"）。共用后天然互斥，也避免"表格在转但按钮可点"造成重复提交；代价是翻页时搜索按钮也会转——语义上正确。
- **为什么行内操作用行 id 而不是布尔值**：操作对象是"某一行"，`togglingId === row.id` 只让被点行转圈；若用整表共用的布尔值，整列按钮会一起转，用户无法判断是哪一行在处理。
- **为什么动作类必须防重入**：Arco 的 `loading` 只表现为不可点击，**不阻止事件重入**。典型场景：搜索按钮与输入框回车是两个入口；`a-popconfirm` 的确定按钮在异步期间仍可能被再次触发。
- **为什么查询类反而不能判空**：用户最新的筛选 / 翻页意图必须生效，正确做法是用请求序号仲裁乱序响应（`fetchSeq`），丢弃新点击会让用户"点了没反应"。

## 2. 存量改造点

### 2.1 `views/LoginLogManagement/LoginLogsView.vue`

无行为改动，仅模板补绑定（`loading` 已存在且已按序号仲裁）：

```vue
<a-button type="primary" :loading="loading" @click="onSearch">搜索</a-button>
<a-button :loading="loading" @click="onReset">重置</a-button>
<a-button size="small" :loading="loading" @click="onRefresh">刷新</a-button>
```

### 2.2 `views/UserManagement/UsersView.vue`

（1）查询类按钮同上，共用既有 `loading`。

（2）行内启停新增 `togglingId`：

```ts
// —— reactive state ——
const togglingId = ref<string | undefined>(undefined)

// —— methods ——
/** 启用 / 禁用 */
async function onToggleStatus(row: UserListItem): Promise<void> {
  if (togglingId.value) return
  const next: UserStatus = row.status === 1 ? 0 : 1
  togglingId.value = row.id
  try {
    await updateUserStatus(row.id, next)
    Message.success(next === 1 ? '已启用' : '已禁用')
    void fetchList()
  } catch {
    // 错误提示已由请求层统一处理
  } finally {
    togglingId.value = undefined
  }
}
```

模板中行内按钮（在 `a-popconfirm` 内）绑定：`:loading="togglingId === (record as UserListItem).id"`。

> 说明：`togglingId` 同时充当"全局互斥"，操作进行中其他行的启停也被挡下。这是刻意的——同一时刻只允许一次写操作，避免并发写与随后的列表刷新互相干扰。

### 2.3 不改动的文件与理由

| 文件 | 不改理由 |
|---|---|
| `ListShowcaseView.vue` | 纯前端同步过滤 / 内存删除 / 本地 CSV 导出，无请求 |
| `FormShowcaseView.vue`、`FormDetailView.vue` | 基于 `useOrderStore` 的内存增删改与路由跳转，无请求 |
| `UserDetailView.vue` | 页面级 `a-spin` 已在用；页面内按钮只有「返回列表」（同步路由跳转） |
| `ComponentShowcaseView.vue` | 纯 UI 组件陈列，无异步操作 |
| `UserFormDrawer.vue`、`FormPageFormView.vue`、`OrderFormDrawer.vue`、`LoginView.vue` | 已符合规则（独立 `submitting` / `loading` + `try/finally` + 防重入） |

## 3. e2e 设计

### 3.1 为什么需要"延迟转发"

本地 dev 请求 10~50ms 返回，loading 态可能在 Playwright 首次轮询（约 100ms）前就消失了，直接断言会 flaky。

解决方式：用 `page.route` 拦截目标接口并**延迟后 `route.continue()`**——请求仍发往真实 dev 后端、响应内容不变，只改变时序。这不构成 `AGENTS.md` §6 所禁止的"打 mock"（不替换响应体），属于测试时序控制。

```ts
/** 延迟指定 URL 的响应转发（不改响应内容，仅用于稳定捕获 loading 态） */
async function delayApi(page: Page, pattern: RegExp, ms: number): Promise<void> {
  await page.route(pattern, async (route) => {
    await new Promise((resolve) => setTimeout(resolve, ms))
    await route.continue()
  })
}
```

> 注意：前端 dev 走 Vite proxy（`/api` → `http://localhost:5080`），Playwright 在浏览器侧拦截，因此按 `5173` 上的 URL 匹配即可（如 `/\/api\/users\?/`）。

### 3.2 用例

| 文件 | 用例 | 断言 |
|---|---|---|
| `user-management.spec.ts` | 点击「搜索」后按钮进入 loading，请求完成后恢复 | 延迟 `GET /api/users?*` → 点击后 `搜索` 按钮有 `arco-btn-loading`，随后消失（按钮可点） |
| `user-management.spec.ts` | 点击「刷新」后按钮进入 loading，请求完成后恢复 | 同上，断言 `刷新` 按钮 |
| `user-management.spec.ts` | 行内「禁用」确认后该行按钮进入 loading | 延迟 `PUT /api/users/{id}/status` → 确认后该行 `禁用` 按钮有 `arco-btn-loading`，完成后消失 |
| `login-log.spec.ts` | 点击「搜索」后按钮进入 loading，请求完成后恢复 | 延迟 `GET /api/login-logs?*` → 断言 `搜索` 按钮 loading 出现后消失 |

断言方式：Arco `a-button` 的 `loading` 会渲染 `arco-btn-loading` class 与加载图标，用 `expect(btn).toHaveClass(/arco-btn-loading/)` / `not.toHaveClass(...)` 判定。

## 4. 不做的事

- 不引入全局 loading 遮罩（`Message.loading` / 全屏 Spin）替代按钮态：按钮态是**就地**反馈，与操作位置绑定，不打断用户。
- 不改请求层（不引入 `AbortController` 取消旧请求）：查询并发由序号仲裁已足够，取消会改变"最后一次意图胜出"的语义。
- 不改任何接口契约、数据流与后端代码。
- 不给 Mock / 同步页面补 loading（避免出现"点了转圈但其实没请求"的假反馈）。

## 5. 风险

| 风险 | 处置 |
|---|---|
| 查询类按钮翻页时也会转圈，是否误导 | 语义正确（同一查询操作），且能有效阻止用户在查询中重复点搜索；如后续认为干扰，可拆分为 `queryLoading` / `tableLoading` 两个状态，但需同步改前端规则 §4.6 |
| `togglingId` 全局互斥导致"其他行点了没反应" | 与"防重入"取舍一致：写操作串行化优先；按钮已 `loading` 不可点，用户不会误以为是卡死 |
| e2e 延迟转发用例在慢机器上仍有 flaky 可能 | 延迟取 600ms，远大于断言轮询间隔；断言 `not.toHaveClass` 用默认 5s 超时兜底 |
