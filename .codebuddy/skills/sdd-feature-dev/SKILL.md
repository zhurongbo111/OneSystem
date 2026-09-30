---
name: sdd-feature-dev
description: 在 OneSystem 仓库按规格驱动开发（SDD）工作流实现功能时使用，覆盖前后端（.NET 8 + Vue 3）的规格创建、最小读取集、实现要点与收尾检查表。当用户提出"实现某功能 / 按规格开发 / 新增某某管理域 / 落地 specs 下某个规格"等诉求时触发。
---

# SDD 功能实现工作流（OneSystem）

本 skill 是 OneSystem 仓库功能实现流程的执行清单：把 `AGENTS.md` §2 的 SDD 工作流落成固定步骤，
使每次实现不必重新推导。**本文件只写流程与章节指针，不复制规则 / 规格正文**（单一事实源，`AGENTS.md` §10）：
判据以 `AGENTS.md`（总则）、`.codebuddy/rules/<端>/RULE.mdc`（专项规则）、`specs/<序号>-<feature>/`（功能规格）为准，
冲突裁决顺序见 `AGENTS.md` §2.3。

## 0. 触发与前置判断

- 适用：新增 / 修改含后端或前端的业务功能（对应 `specs/<序号>-<feature>/`）。
- 纯工程调整（构建、部署、规则维护）不适用本 skill，直接按 `AGENTS.md` 对应章节执行。
- 动手前先确认改动面：只涉及后端 / 前端 / 两端，决定步骤 3 读哪份专项规则、步骤 5 跑哪些测试。

## 1. 规格先行（强制）

1. 查 `specs/` 下是否已有对应 `<序号>-<feature>/`（功能名不带序号；序号 = 现存最大 + 1，见 `AGENTS.md` §2.1）。
2. 新建规格：三文件（`requirement.md` / `design.md` / `tasks.md`）一并写入，YAML frontmatter
   `created = updated =` 当日；变更已有规格：改正文时刷新三文件 `updated`（等价改写不刷新，`AGENTS.md` §2.5）。
3. `design.md` 必须定义：API 设计（路由 / 入参 / 出参 / 业务错误码）、数据模型与表结构（含字段约束表）、
   前端交互、技术决策；读模型须进「仓储接口表」（后端规则 §4.3）；权限点 / 错误码扩展在此登记
   （`AGENTS.md` §4.2；权限点清单唯一来源 `specs/028-erp-rbac/design.md` §0.2）。
4. 拆 `tasks.md`：可勾选 `- [ ]` 任务项，顺序即实现顺序；含前后端改动时任务须包含「e2e 覆盖可感知行为」项
   （`AGENTS.md` §6 强制门槛）。
5. ERP 新功能接续前先读 `specs/ROADMAP.md`（模块边界 / 前置决策）。

## 2. 实现前最小读取集（`AGENTS.md` §2.4，禁止全仓探索）

固定四项，按序读取：

1. `.codebuddy/CONTEXT.md` — 项目结构摘要（替代代码探索）；
2. 本功能 `specs/<序号>-<feature>/design.md` + `tasks.md`；
3. 专项规则（只读改动面涉及的端）：
   - 后端 → `.codebuddy/rules/backend/RULE.mdc`（端内读取集见其 §3.1）；
   - 前端 → `.codebuddy/rules/frontend/RULE.mdc`（端内读取集见其 §2.1）；
4. 本次要改动的目标文件 + 直接调用的既有文件（同域既有同类文件即参照实现，见 CONTEXT.md §2 / §3「基准参照」）。

禁止：为"了解项目"遍历目录、读与本功能无关的 Feature / views 域、通读 `Migrations/`。

## 3. 后端实现要点（判据与正文见后端规则，此处只列执行顺序）

按 `tasks.md` 顺序执行，典型一个 API 用例的落地顺序：

1. 实体与约束：`Entities/<实体>.cs` + `<实体>FieldConstraints.cs`（字段约束**单一来源**，规则 §5.3）；
2. EF 配置：`Persistence/Configurations/<实体>Configuration.cs`（取常量，禁字面量）；表结构变更走 EF Core Migrations（§5.1）；
3. 仓储：`Abstractions/I<实体>Repository.cs` + `Infrastructure/Repositories/<实体>Repository.cs`；
   含联查字段才建读模型（`Abstractions/`，`sealed record`，§4.3）；
4. 用例四件套：`Features/<Feature>/<Action>/`（Request / RequestValidator / RequestHandler / Response，§4.1）；
   格式校验只进 Validator，查库约束进 Handler；业务失败抛 `BusinessException` + `Errors/ErrorCode` 常量；
   跨仓储写用 `IUnitOfWork` 事务包裹（§4.4）；时间字段一律 `DateTimeOffset`（§5.2）；
5. 正向 DTO 映射集中 `Features/<Feature>/<Feature>DtoMapper`（§4.3），禁止 Handler 内联拼 DTO；
6. Controller：只注入 `IMediator`，路由 `/api/<资源复数>`（§4.2）；`[RequirePermission]` 权限标注
   （`028` 默认拒绝，未标注动作即 `40300`）；写用例接入操作日志（`029` §0.1 清单续行）；
7. DI 注册：`Core/DependencyInjection.cs`（用例 + 校验器），新仓储加 `Infrastructure/DependencyInjection.cs`（§4.5）；
8. 单测：`tests/App.Tests/`，RequestHandler 公共方法必须有正常 + 异常分支用例（§9）；字段约束追加
   `FieldValidationConsistencyTests`；仓储测试优先行为型假实现（`*TestDoubles`）；
9. 含聚合的仓储查询交付前须有打真实关系型库的验证（§5.4，InMemory 测不出）。

## 4. 前端实现要点（判据与正文见前端规则，此处只列执行顺序）

1. 接口层：`src/api/<entity.ts>`（归属判定 §3；TS 类型与后端 DTO camelCase 一一对应）；
2. 视图域：`views/<Domain>/` 与后端 `Features/`、路由前缀、菜单、e2e spec 四者对齐，域内平铺（§4.1）；
   列表页参照 `views/Showcase/ListShowcaseView.vue`，表单 / 详情参照同域既有文件（列表复数 / 表单详情单数）;
3. 组合式分区书写（§4.5，正文 `specs/008-composable-style/design.md` §0）；模板 ref 命名 `xxxRef`；
4. 列表页 / 操作列 / 按钮 loading / 表单详情 / 图标：判据在前端规则 §4.6 / §4.7 / §5 / §5.2 / §5.5，
   **正文在对应规格 §0**（对照表见 CONTEXT.md §6）；新增列表页复制 showcase 结构再替换业务字段；
5. 路由与菜单：`router/index.ts` 注册（懒加载 + `ROUTE_PERMISSIONS`）+ `AppLayout.vue` 菜单项（带权限）；
   仅新增页面 / 菜单时才动这两个文件（前端规则 §2.1）；
6. e2e：`e2e/<功能域>.spec.ts`（kebab-case 短名，不带 `-management`，§10）；稳定性强制约定走 helpers
   （`clickUntil` / `searchAndWaitHit` / `expectMessage` / `loginAs`，§10.1），禁止加载态中直接点击。

## 5. 测试门槛（`AGENTS.md` §6，未通过不得声明完成）

- 只改一端：至少跑被改动端；两端都改：`cd backend; dotnet test` 与前端 e2e 都要跑通；
- 前端 e2e 一律 `cd frontend; npm run e2e:run`（每轮独立临时库，跑完自动删库；**禁止**手工起服务连开发库跑全量，
  CONTEXT.md §6）；迭代单文件用 `npm run e2e:run -- -Spec e2e/<域>.spec.ts`；
- 前端改动必须新增 / 更新 e2e 覆盖可感知行为（含按钮 loading 断言），无 e2e 不得勾选任务；
- e2e 失败先按前端规则 §10.1 排查时序问题（看 `test-results/<用例名>/error-context.md`），再怀疑业务实现。

## 6. 收尾检查表（逐项确认，全过才算完成）

- [ ] `tasks.md` 任务项全部勾选；
- [ ] 规格三文件 frontmatter `updated` 已按 §2.5 口径处理；
- [ ] 结构有变化（新增实体 / 用例 / 功能域 / api 文件 / 命令 / 端口）→ 同步更新 `.codebuddy/CONTEXT.md` 对应章节
      （§2.4 强制，随当次变更一起提交）；
- [ ] 错误码 / 权限点 / 导出范围等登记项已写入对应 `design.md`（`027` §0.1、`028` §0.2、`029` §0.1、ROADMAP §6 区间续行）；
- [ ] 提交：Conventional Commits（`feat|fix|test|...`，scope 用 `frontend` / `backend` / 模块名）；
      **规格文件变更单独提交** `docs(specs): ...`（`AGENTS.md` §8.2）；
- [ ] 分支：在 `feature/<功能名>`（不带序号）上开发，完成后合回 `main` 并删除分支（§8.1）。
