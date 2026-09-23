---
created: 2026-09-17
updated: 2026-09-17
---

# 任务清单：客户价格、账期与信用额度（erp-partner-price）

> 依据 `specs/036-erp-partner-price/design.md` 拆分。含客户价档案 + 往来单位账期 / 额度 + 销售开单取价 + 信用校验 + 往来对账逾期。按顺序实现，完成后勾选。
> 前置：`023`（收付款与往来对账、应收口径）已实现；`012` / `013`（商品 / 往来单位）已实现。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 一、后端：数据模型与仓储

- [x] 1.1 新增实体 `PartnerPrice` + EF 配置（唯一索引 `(PartnerId, ProductId)`、外键）+ `AppDbContext` `DbSet`
- [x] 1.2 `Partner` 实体 / 配置追加 `PaymentTermDays` / `CreditLimit`（默认 0）；`PartnerFieldConstraints` 追加 `PaymentTermDaysMaxValue = 3650`、`CreditLimitMaxValue`
- [x] 1.3 增量迁移 `dotnet ef migrations add AddErpPartnerPrice -p src/App.Infrastructure -s src/App.Api`
- [x] 1.4 读模型 `PartnerPriceListItem` / `EffectivePriceItem`；`ReconciliationItem` 追加 `PaymentTermDays` / `EarliestDueDate` / `MaxOverdueDays` / `OverdueOrderCount`
- [x] 1.5 `IPartnerPriceRepository` + 实现（`GetByIdAsync` / `ExistsAsync` / `GetPagedAsync`（联查客户与商品）/ `GetEffectiveAsync`（批量取价 + 来源）/ `AddAsync` / `UpdateAsync` / `DeleteAsync`）+ 注册
- [x] 1.6 `ISettlementQueryRepository` 追加 `GetReceivableAmountAsync(partnerId)`（`023` 口径）+ 实现
- [x] 1.7 `ErrorCode.cs` 追加 `40130 CreditLimitExceeded` / `40131 PartnerPriceExists`

## 二、后端：用例与接口

- [x] 2.1 共享出参 `Features/PartnerPrices/PartnerPriceListItemDto.cs` / `PartnerPriceDetailDto.cs` / `EffectivePriceDto.cs` + `PartnerPricesDtoMapper.cs`
- [x] 2.2 新增用例 `PartnerPrices/GetPartnerPrices`、`CreatePartnerPrice`、`GetPartnerPriceById`、`UpdatePartnerPrice`、`DeletePartnerPrice`、`GetEffectivePrices`
- [x] 2.3 `PartnerPricesController`（6 端点，`effective` 在 `{id:guid}` 之前）+ DI 注册
- [x] 2.4 `Partners/UpdatePartner` + DTO 追加 `paymentTermDays` / `creditLimit`（全量覆盖语义）
- [x] 2.5 `Sales/CreateSalesShipment` 追加信用校验（额度 > 0 时校验「应收 + 本单 ≤ 额度」，位置在扣库存之前）
- [x] 2.6 `Settlements/GetReconciliation` 追加账期与逾期字段（含 `overdueOnly` 筛选）；`023` 的 `GetReconciliationRequest` 与读模型同步

## 三、单元测试

- [ ] 3.1 客户价用例：CRUD / 客户与商品校验（`40400` / `40108` / `40107` / 非客户 `40000`）/ 重复 `40131` / 列表筛选与映射
- [ ] 3.2 `GetEffectivePrices`：协议价 / 默认价 / 相等时算协议价 / `40400` / 去重与上限
- [ ] 3.3 信用校验：额度 0 跳过（不查应收）；通过 / `40130`（含额度、应收、本单）；失败路径无库存 / 流水 / 单据变化
- [ ] 3.4 账期与逾期：`EarliestDueDate` / `MaxOverdueDays` / `OverdueOrderCount` 计算与边界（账期 0、已结清不逾期）；`overdueOnly` 过滤
- [ ] 3.5 往来单位：账期 / 额度全量覆盖（缺字段清 0）与边界
- [ ] 3.6 字段约束一致性（价格与额度精度、`PaymentTermDaysMaxValue`、`keyword` 50-51）
- [ ] 3.7 `cd backend && dotnet build` / `dotnet test` 通过（既有用例全部回归）

## 四、前端

- [ ] 4.1 `src/api/partnerPrice.ts`（6 个接口 + 类型）；`api/partner.ts` 扩展账期 / 额度；`api/sale.ts` 不变（单价仍由页面传入）
- [ ] 4.2 `views/PartnerPriceManagement/PartnerPricesView.vue`（筛选 + 协议价 / 销售价对比 + 高价格提示 + 删除）
- [ ] 4.3 `views/PartnerPriceManagement/PartnerPriceFormDrawer.vue`（新增 / 编辑，客户与商品不可改）
- [ ] 4.4 `PartnerFormDrawer.vue` 追加账期天数 / 信用额度（含「0 = 现结 / 不限」提示）
- [ ] 4.5 `SaleFormPage.vue` 批量取价 + 单价默认填充 + 来源标注（协议价 / 默认价）+ 切客户重新取价 + `pricesLoading`
- [ ] 4.6 `ReconciliationView.vue` 追加账期 / 最早到期日 / 最大逾期天数列与「仅看逾期」筛选；下钻抽屉追加到期日列
- [ ] 4.7 `SaleDetailView.vue` 追加到期日描述项（后端返回值）
- [ ] 4.8 `router/index.ts` 新增 `partner-prices`；`AppLayout.vue`「资金」分组追加「客户价格」
- [ ] 4.9 `027` 导出范围表续行（客户价格列表导出）+ 导出接入
- [ ] 4.10 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 五、E2E（Playwright）

- [ ] 5.1 新增 `e2e/partner-price.spec.ts`：配置协议价 → 销售开单自动带出协议价且标注「协议价」
- [ ] 5.2 同文件：未配置协议价的商品为「默认价」；删除协议价后回到销售价
- [ ] 5.3 同文件：额度 1000 / 应收 800 → 开 300 被拒（提示可见）、开 200 通过
- [ ] 5.4 同文件：额度 0 不限（大额开单通过）；往来对账逾期列与「仅看逾期」筛选
- [ ] 5.5 `cd frontend && npm run test:e2e` 全量通过（含 `sale` / `settlement` / `partner-management` 既有用例回归）

## 六、规格与上下文联动

- [ ] 6.1 `specs/023-erp-settlement/design.md` 加注记：`ISettlementQueryRepository` 追加 `GetReceivableAmountAsync`；对账读模型追加逾期字段（§5 范围外的账期 / 额度由 `036` 落地）
- [ ] 6.2 `specs/013-erp-partner/design.md` 加注记：`Partners` 追加账期 / 额度字段
- [ ] 6.3 `specs/016-erp-sale/design.md` 加注记：开单页取价优先级与来源标注、创建时信用校验
- [ ] 6.4 `specs/028-erp-rbac/design.md` §0.2 已登记 `partnerPrices.*`（确认无需改动）
- [ ] 6.5 `specs/029-erp-audit-log/design.md` §0.1 续行：客户价创建 / 更新 / 删除、往来单位账期 / 额度变更摘要
- [ ] 6.6 `.codebuddy/CONTEXT.md` §2（PartnerPrices 实体 / 仓储 / 读模型 / 错误码）、§3（PartnerPriceManagement 域、api 文件）、§6 同步
- [ ] 6.7 `specs/ROADMAP.md` 状态列更新（`036` → 已实现）

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 口径一致：额度校验用的应收与往来对账页展示的应收来自同一仓储方法（单测断言）。
