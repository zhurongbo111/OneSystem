---
created: 2026-09-17
updated: 2026-09-17
---

# 任务清单：仓库调拨（erp-transfer）

> 依据 `specs/039-erp-transfer/design.md` 拆分。含后端新单据域（双仓库存 + 四类流水）+ 4 个用例 + 前端列表 / 开单 / 详情。按顺序实现，完成后勾选。
> 前置：`038`（多仓）已实现；`019`（流水）/ `026`（成本）已实现。
> **待用户确认**：一期不含「在途」（路线图原文含在途），见 `requirement.md` §5 与 `design.md` §5；确认后再开工。
> 后端：`cd backend`（`dotnet build` / `dotnet test`）。前端：`cd frontend`（`npm run lint` / `type-check` / `build` / `test:e2e`）。
> 提交 / 合并不列入待办：由用户主动发起指示。

## 零、前置门禁

- [ ] 0.1 **用户确认**「一期一步式调拨（不含在途）」的范围裁剪；确认后进入阶段一

## 一、后端：数据模型与仓储

- [ ] 1.1 新增 `Transfer` / `TransferItem` 实体（枚举复用 `OrderStatus`）；EF 配置 + `AppDbContext` 2 个 `DbSet`
- [ ] 1.2 增量迁移 `dotnet ef migrations add AddErpTransfer -p src/App.Infrastructure -s src/App.Api`
- [ ] 1.3 `StockMovementType` 追加 `TransferOut = 11` / `TransferIn = 12` / `TransferOutVoid = 13` / `TransferInVoid = 14`
- [ ] 1.4 `ITransferRepository` + 实现（`GetPagedAsync`（转出 / 转入仓筛选）/ `GetDetailAsync` / `AddAsync` / `UpdateStatusAsync` / `GenerateTransferNoAsync`）+ 注册
- [ ] 1.5 `ErrorCode.cs` 追加 `40126 TransferSameWarehouse`

## 二、后端：用例与接口

- [ ] 2.1 共享出参 `Features/Transfers/TransferListItemDto.cs` / `TransferDetailDto.cs` + `TransfersDtoMapper.cs`
- [ ] 2.2 新增用例 `Transfers/GetTransfers`（Request + Validator + Handler + Response）
- [ ] 2.3 新增用例 `Transfers/CreateTransfer`（仓校验 → 商品快照 → 转出（含成本与流水）→ 转入（同单价）→ 落单；同一事务）
- [ ] 2.4 新增用例 `Transfers/GetTransferById`
- [ ] 2.5 新增用例 `Transfers/VoidTransfer`（双向回冲 + 两条反向流水 + 状态）
- [ ] 2.6 `TransfersController`（4 端点）+ DI 注册
- [ ] 2.7 `019` §0 文案表续行（4 个新类型的文案与颜色）

## 三、单元测试

- [ ] 3.1 `CreateTransfer` 成功：单号 / 快照 / 统计 / 调用顺序 / 两条流水单价相同与仓正确 / `Commit`
- [ ] 3.2 `CreateTransfer` 异常：同仓 `40126`、转出不足 `40103`（无转入发生 + `RollbackAsync`）、仓 `40400` `40123`、商品 `40400` `40107`、明细空 `40110`、`Commit` 异常回滚
- [ ] 3.3 `VoidTransfer`：双向回冲 + 反向流水 + 原单价还原 + 已作废 `40104` / 不存在 `40400` / 缺原价兜底
- [ ] 3.4 `GetTransfers`（转出 / 转入仓与日期筛选 / 分页）、`GetTransferById`（明细快照 / `40400`）
- [ ] 3.5 对账一致性：调拨 + 作废后按仓 `Σ 流水 == 该仓库存`；组织级数量与成本总额不变
- [ ] 3.6 字段约束一致性（`TransferNo` 20 / 明细列长 / `quantity` / `keyword`）
- [ ] 3.7 `cd backend && dotnet build` / `dotnet test` 通过（既有用例全部回归）

## 四、前端

- [ ] 4.1 `src/api/transfer.ts`（类型 + 4 个请求函数）
- [ ] 4.2 `views/TransferManagement/TransfersView.vue`（筛选行含双仓 + 表格 + 作废 + 分页）
- [ ] 4.3 `TransferFormPage.vue`（双仓下拉 + 明细子表格 + 按转出仓查可用库存 + 数量合计 + `submitting`）
- [ ] 4.4 `TransferDetailView.vue`（表头 + 明细只读 + 作废 + 404 空态）
- [ ] 4.5 `router/index.ts` 新增 `transfers` / `transfers/new` / `transfers/detail/:id`；`AppLayout.vue`「库存」分组追加「调拨单」+ `MENU_ROUTE_MAP` 增加 `transferDetail`
- [ ] 4.6 `027` 导出范围表续行（调拨列表导出，2 工作表）+ 导出函数接入
- [ ] 4.7 `cd frontend && npm run type-check` / `npm run lint` / `npm run build` 全绿

## 五、E2E（Playwright）

- [ ] 5.1 新增 `e2e/transfer.spec.ts`：A 仓 → B 仓调拨 5 件 → 库存查询按仓验证（A −5 / B +5）
- [ ] 5.2 同文件：流水页出现 `-5`「调拨转出」（A 仓）与 `+5`「调拨转入」（B 仓）
- [ ] 5.3 同文件：作废 → 双仓回冲 + 两条反向流水；同仓调拨被拒（提示）；A 仓不足被拒（提示含仓名）
- [ ] 5.4 同文件：列表筛选（转出仓 / 转入仓 / 日期）与详情展示
- [ ] 5.5 `cd frontend && npm run test:e2e` 全量通过（含 `warehouse` / `inventory-management` / `stock-movement` 既有用例回归）

## 六、规格与上下文联动

- [ ] 6.1 `specs/019-erp-stock-movement/design.md` §0 续行 4 个新类型（随本规格起草已写入）
- [ ] 6.2 `specs/038-erp-multi-warehouse/design.md` §0 加注记：调拨产生「两条不同仓流水」的实例
- [ ] 6.3 `specs/ROADMAP.md` §6.7 单号前缀表：`TR` 行标记为已启用（`039`）
- [ ] 6.4 `specs/ROADMAP.md` §4.2 加注记：调拨（`039`）已落地；在途留待后续（与 `024` 模板统一）
- [ ] 6.5 `.codebuddy/CONTEXT.md` §2（Transfers Feature / 仓储 / 枚举）、§3（TransferManagement 域、api 文件）、§6 同步
- [ ] 6.6 `specs/ROADMAP.md` 状态列更新（`039` → 已实现）

## 完成定义

- 上述任务全部勾选，且 `AGENTS.md` §6 强制测试门槛通过（后端 `dotnet test` + 前端 `npm run test:e2e` 全绿）。
- 数量守恒与成本守恒双断言通过：调拨不改变组织级总数量与总成本。
