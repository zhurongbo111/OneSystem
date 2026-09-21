---
created: 2026-09-10
updated: 2026-09-16
---

# 需求规格：按钮交互反馈（loading）

> **约定正文见本规格 `design.md` §0（唯一事实源）**；前端规则 `.codebuddy/rules/frontend/RULE.mdc` §4.6 只留判据与指针（`AGENTS.md` §2.3 / §10）。

## 背景

前端列表页与表单的异步操作缺少统一的交互反馈约定：

- **查询类按钮无反馈**：`UsersView.vue` / `LoginLogsView.vue` 的「搜索」「重置」「刷新」点击后只有表格遮罩在转，按钮本身没有任何变化；用户无法判断请求是否已发出，也不知道能不能再点。
- **行内异步操作无反馈**：`UsersView.vue` 的「启用 / 禁用」点击确认后按钮无变化，请求期间可被重复确认，重复发出写请求。
- **命名与绑定方式不统一**：表单提交、重置密码、登录已各自用了 `submitting` 之类的状态，但绑定位置、是否 `try/finally` 复位、是否防重入各不相同，也没有沉淀为规则。

需要特别说明的是：**并发无法靠"期望"消除**。HTTP 不把同一域名的请求串行化、事件回调互不阻塞、`void fetchList()` 显式不等待，因此"点击即反馈 + 进行中不可重复触发"必须在 UI 层显式实现，不能依赖"同一时间只会有一个请求"这个不成立的假设。

## 目标

- 任何会**发起请求并等待结果**的按钮，点击后**立即**进入 loading 状态，请求结束（成功或失败）后恢复。
- loading 绑定到「操作」而非「按钮」：同一操作的所有触发入口共用同一个状态；不同操作状态互相独立，不连带转圈。
- 操作进行中不可重复触发（动作类）。
- 把约定沉淀为前端规则并让存量页面与之对齐，同时用 e2e 覆盖用户可感知的 loading 行为。

## 功能点

1. **约定正文**：落在本规格 `design.md` §0（状态命名与绑定表 + 实现要求 + e2e 门槛）；查询并发与请求序号的关系见 `specs/006-list-showcase/design.md` §0「交互模式」。
2. **存量改造**：
   - `views/LoginLogManagement/LoginLogsView.vue`：搜索 / 重置 / 刷新按钮绑定查询 `loading`。
   - `views/UserManagement/UsersView.vue`：搜索 / 重置 / 刷新按钮绑定查询 `loading`；行内启用 / 禁用按钮绑定新增的 `togglingId`，并在 handler 内防重入。
   - 表单提交类（`UserFormDrawer.vue`、`Showcase/FormPageFormView.vue`、`Showcase/OrderFormDrawer.vue`、`LoginView.vue`）已符合规则，**无需改动**。
   - 纯同步页面（`ListShowcaseView.vue`、`FormShowcaseView.vue`、`FormDetailView.vue`、`UserDetailView.vue`、`ComponentShowcaseView.vue`）不发起需等待的请求（或只有路由跳转 / 本地导出等同步动作），按规则**不置 loading**，保持不变。
3. **e2e**：为查询按钮与行内操作按钮的 loading 行为补用例（`frontend/e2e/user.spec.ts`、`frontend/e2e/login-log.spec.ts`）。

## 验收标准

1. 规格 `design.md` §0 含「按钮交互反馈」约定（操作级绑定 + 命名表 + 实现要求 + e2e 门槛）。
2. `LoginLogsView.vue` 的搜索 / 重置 / 刷新按钮点击后进入 loading，请求结束后恢复可点。
3. `UsersView.vue` 的搜索 / 重置 / 刷新按钮点击后进入 loading，请求结束后恢复可点。
4. `UsersView.vue` 的启用 / 禁用按钮点击确认后，**仅该行**按钮进入 loading；操作进行中重复确认不再发出请求。
5. 查询类操作不因 loading 丢弃新点击（保留 `fetchSeq` 请求序号仲裁，行为不变）。
6. `npm run lint` 通过。
7. `npm run type-check` 通过。
8. `npm run test:e2e` 全量通过（含新增 loading 用例）。

> 本规格不涉及后端代码改动（后端仅补充规则文档，见 `specs/009-user-management/design.md` §2.2 既有约定）。
