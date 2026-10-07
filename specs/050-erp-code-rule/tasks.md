---
created: 2026-10-07
updated: 2026-10-07
---

# 任务清单：主数据编码规则（erp-code-rule）

> 依据 `specs/050-erp-code-rule/design.md` 拆分。新增编码规则配置表 + 生成能力 + 改造商品创建 + 前端配置页 + e2e。
> 前置：`012`（商品 `Code` / `40101`）、`028`（权限）、`029`（操作日志）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与迁移

- [ ] 1.1 实体 `CodeRule` + 枚举 `CodeResourceType` / `CodeDatePattern` + `CodeRuleFieldConstraints`；`AppDbContext` 追加 `CodeRules`
- [ ] 1.2 EF 配置（`ResourceType` 唯一、列类型 / 列长）
- [ ] 1.3 增量迁移 `dotnet ef migrations add AddErpCodeRule -p src/App.Infrastructure -s src/App.Api`

## 二、后端：仓储与生成器

- [ ] 2.1 `ICodeRuleRepository`（`GetAllAsync` / `GetByResourceAsync` / `UpsertAsync` / `ReserveSerialAsync`）+ 实现 + 注册
- [ ] 2.2 `ReserveSerialAsync` 事务内行锁自增（`LastKey` 切换重置）
- [ ] 2.3 `ICodeGenerator` + `CodeGenerator`（`App.Core/Alerts`→`App.Core/` 语义位置；`40181`）+ 注册
- [ ] 2.4 读模型 `CodeRuleItem` + `CodeRuleDtoMapper`

## 三、后端：用例与接口

- [ ] 3.1 `CodeRules/GetCodeRules` / `SaveCodeRule` + `CodeRulesController`（`/api/code-rules`）+ DI
- [ ] 3.2 改造 `Products/CreateProduct`：`code` 可空 → 生成（`40181`）/ 重试（`40182`）/ 非空走原路径
- [ ] 3.3 `CreateProductRequest` 的 `code` 改为可空（Validator 调整）
- [ ] 3.4 写用例接入操作日志（`029` §0.1 续行，资源 `CodeRule`）

## 四、后端：错误码

- [ ] 4.1 `ErrorCode.cs` 追加 `40181` / `40182`（§3.2）；`specs/ROADMAP.md` §6 顶部「下一个可用」更新为 `40183`

## 五、单元测试（后端）

- [ ] 5.1 生成格式：各 `DatePattern` × `SerialLength`、前缀空 / 非空、补零
- [ ] 5.2 重置：连续自增、跨日 / 跨月重置
- [ ] 5.3 并发：并发生成不重号
- [ ] 5.4 商品接入：生成 / `40181` / 撞码 `40182` / 非空回归
- [ ] 5.5 规则保存：越界 / 非法字符 `40000`、不重置序号
- [ ] 5.6 字段约束一致性单测
- [ ] 5.7 `cd backend && dotnet build` / `dotnet test` 通过

## 六、前端

- [ ] 6.1 `api/codeRule.ts`（+ `CODE_DATE_PATTERN_META`）；`api/product.ts` 的 `code` 改可选
- [ ] 6.2 `views/CodeRuleManagement/CodeRulesView.vue`（前缀 / 日期段 / 位数 + 实时预览）
- [ ] 6.3 `views/ProductManagement/ProductFormDrawer.vue`：编码可留空 + 提示
- [ ] 6.4 `router/index.ts` 新增路由；`AppLayout.vue`「系统」分组续行「编码规则」+ `MENU_ROUTE_MAP`
- [ ] 6.5 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 七、E2E（Playwright）

- [ ] 7.1 新增 `e2e/code-rule.spec.ts`：配规则 → 建商品留空编码 → 自动生成且连续递增
- [ ] 7.2 同文件：未配规则留空 → `40181`；填编码 → 成功
- [ ] 7.3 `cd frontend && npm run test:e2e` 全量通过（含商品域回归）

## 八、规格与上下文联动

- [ ] 8.1 `specs/012-erp-product/design.md` §3.4 / §3.5 / §4.4 改写为最终态（`code` 可空）+ 演进指针；刷新 `updated`
- [ ] 8.2 `specs/028-erp-rbac/design.md` §0.2 续行 `codeRules.*`；刷新 `updated`
- [ ] 8.3 `specs/025-erp-report/design.md` §0.2「系统」分组续行「编码规则」；刷新 `updated`
- [ ] 8.4 `.codebuddy/CONTEXT.md` §2（实体 / 枚举 / 仓储 / 生成器）/ §3（新增域、菜单、api）同步
- [ ] 8.5 `specs/ROADMAP.md` §4.2 状态更新（`050` → 已实现）；§3 覆盖矩阵「L2 主数据 · 物料 / 往来 / 分类」缺口同步；§6 错误码占用续行

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- P7 阶段（`046`–`050`）全部交付；商品编码留空可自动生成，填值行为与升级前一致。
