---
created: 2026-09-13
updated: 2026-09-20
---

# 任务清单：往来单位（erp-partner）

> 依据 `specs/013-erp-partner/design.md` 拆分；按顺序实现，完成后勾选。
> 后端：`cd backend`；前端：`cd frontend`。分支：`feature/erp-inventory`（当前分支，用户确认不另切分支）。

## 一、后端

### 1.1 实体与数据模型

- [x] 1.1.1 新增枚举 `App.Core/Entities/PartnerType.cs`、`PartnerStatus.cs`
- [x] 1.1.2 新增实体 `App.Core/Entities/Partner.cs`（Name 唯一、Type、Contact、Phone、Address、Status、审计字段）
- [x] 1.1.3 新增字段约束常量：`PartnerFieldConstraints`（值见 design §2.3）
- [x] 1.1.4 新增 EF 实体配置 `Persistence/Configurations/PartnerConfiguration.cs`：列类型 / 长度 / 索引（Name 唯一）
- [x] 1.1.5 `AppDbContext` 注册 `Partners` `DbSet`
- [x] 1.1.6 生成迁移 `AddErpPartner` 并在 dev 库 `dotnet ef database update` 验证（表 / 索引 / 唯一约束）

### 1.2 仓储接口与实现

- [x] 1.2.1 `IPartnerRepository` 接口 + `PartnerRepository`（GetById / ExistsByName（忽略大小写）/ GetPaged（keyword 匹配名称 / 联系人）/ Add / Update）
- [x] 1.2.2 审计字段由 Handler 经 `ICurrentUser` 获取后随实体传入仓储

### 1.3 往来单位 API

- [x] 1.3.1 `Partners` 用例 5 个：GetPartners / CreatePartner（40102）/ GetPartnerById / UpdatePartner（不可改 Name）/ UpdatePartnerStatus + Validator
- [x] 1.3.2 `PartnersController`（5 路由）+ DI 注册（仓储 + Handler + Validator）

### 1.4 错误码与 Swagger

- [x] 1.4.1 `ErrorCode.cs` 追加 40102（常量 + 注释，见 design §3.2）
- [x] 1.4.2 Swagger **不分组**（用户已确认）：维持现有单文档 Swagger，不使用 `ApiExplorerSettings.Group`

### 1.5 单元测试

- [x] 1.5.1 往来单位用例测试（Create 重名 40102 / Update 不改 Name / 筛选传参 / Get 40400）
- [x] 1.5.2 扩展 `FieldValidationConsistencyTests`（EF 长度 == 常量；Validator 边界通过 / 拒绝；keyword ≤ 50）
- [x] 1.5.3 `dotnet test` 全绿；`dotnet build` 0 警告

## 二、前端

### 2.1 接口层

- [x] 2.1.1 `src/api/partner.ts`（往来单位类型 / 请求；导出 `getPartners` 供开单下拉复用）

### 2.2 往来单位页面

- [x] 2.2.1 `PartnerFormDrawer.vue`（新增 / 编辑 / 查看三态；编辑态 Name 只读——往来单位不做独立详情页，查看用抽屉 disabled 态，见 design §5）
- [x] 2.2.2 `PartnersView.vue`（类型 / 关键词 / 状态筛选 + 表格 + 操作列 + 分页）

### 2.3 路由与菜单

- [x] 2.3.1 `router/index.ts` 新增 `partners` 路由
- [x] 2.3.2 `AppLayout.vue` 「进销存」分组追加子项「往来单位」（分组由 erp-product 创建；若 erp-partner 先交付则本规格同时创建分组，key `erp`、图标 `IconStorage`、默认展开）

### 2.4 前端质量

- [x] 2.4.1 `npm run lint` 0 error；`npm run build` 成功
- [x] 2.4.2 手动联调走查：建供应商 / 客户 → 类型筛选 → 重名提示 → 编辑 / 启停 → 停用后 `status=1` 查询不含该单位，全链路无 console 报错（已由 e2e 全量覆盖）

## 三、E2E（Playwright）

### 3.1 往来单位

- [x] 3.1.1 `e2e/partner.spec.ts`：登录 → 建供应商 / 客户（重名 40102 提示）→ 类型筛选 → 停用后按 `status=1` 查询不含该单位
- [x] 3.1.2 `npm run test:e2e` 全绿（含既有用例回归，68 passed）

## 四、交付

- [x] 4.1 规格三件套最终一致性复查（代码与 design 表结构 / 错误码 / 路由逐条对照）
> 提交 / 合并不列入待办：由用户主动发起指示（用户约定 2026-09-13）。

## 五、变更（2026-09-20）：往来单位类型只放宽不收窄

> 需求 / 设计见 `requirement.md` F4、`design.md` §3.2 / §3.4 / §4.4 / §5 / §6。

- [x] 5.1 后端：`ErrorCode` 追加 `40119 PartnerTypeNarrowingNotAllowed`
- [x] 5.2 后端：`UpdatePartnerRequestHandler` 增加「类型只放宽不收窄」拦截（保持原类型或 `Both`，否则 `40119`）
- [x] 5.3 后端：`UpdatePartnerRequestHandlerTests` 补收窄拒绝 / 放宽通过用例
- [x] 5.4 前端：`PartnerFormDrawer.vue` 编辑态按原类型禁用会收窄的类型项并给出说明
- [x] 5.5 E2E：`partner.spec.ts` 补「编辑供应商时客户项不可选、可放宽为两者」
- [x] 5.6 验证：`dotnet test`（531 通过）与 `npm run e2e:run`（121 通过）通过
