using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Auth;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Categories.CreateCategory;
using App.Core.Features.Costs;
using App.Core.Features.Costs.RecalculateCosts;
using App.Core.Features.Partners.UpdatePartner;
using App.Core.Features.Partners.UpdatePartnerStatus;
using App.Core.Features.Products.CreateProduct;
using App.Core.Features.Products.UpdateProduct;
using App.Core.Features.SalesShipments.CreateSalesShipment;
using App.Core.Features.PurchaseOrders.ClosePurchaseOrder;
using App.Core.Features.PurchaseReceipts.VoidPurchaseReceipt;
using App.Core.Features.Roles.UpdateRole;
using App.Core.Features.Settlements.VoidSettlement;
using App.Core.Features.StockTakes.CreateStockTake;
using App.Core.Features.Users.ResetPassword;
using App.Core.Features.Users.UpdateUser;
using App.Infrastructure;
using App.Infrastructure.Auth;
using App.Infrastructure.Persistence;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 写用例的操作审计接入测试（specs/029-erp-audit-log tasks.md 4.2 / 4.3 / 4.4）：
/// 每个资源域至少覆盖一个写用例的资源类型 / 动作 / 对象快照 / 摘要 / 差异内容；
/// 业务规则拒绝时不产生日志；日志在提交前写入（失败回滚不留痕）。
/// </summary>
public class AuditLogWriteIntegrationTests
{
    private static readonly Guid _operatorId = Guid.NewGuid();

    // ===== 基础档案域 =====

    /// <summary>商品域：编辑商品改价，摘要含业务标识、差异含「采购价 10.00 → 8.80」</summary>
    [Fact]
    public async Task UpdateProduct_RecordsProductUpdateWithPriceChange()
    {
        var context = TestSupport.CreateDbContext();
        var category = await SeedCategoryAsync(context);
        var product = TestSupport.NewProduct(code: "SKU-001", name: "螺丝", purchasePrice: 10m, categoryId: category.Id);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var audit = new RecordingAuditLogger();
        await new UpdateProductRequestHandler(
            new ProductRepository(context),
            new CategoryRepository(context),
            new InventoryRepository(context),
            new UnitOfWork(context),
            new StubCurrentUser(_operatorId),
            audit).HandleAsync(new UpdateProductRequest
            {
                Id = product.Id,
                Name = "螺丝",
                CategoryId = category.Id,
                Unit = "个",
                PurchasePrice = 8.8m,
                SalePrice = 12m,
                SafetyStock = 5,
            });

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditResource.Product, entry.Resource);
        Assert.Equal(AuditAction.Update, entry.Action);
        Assert.Equal(product.Id, entry.ResourceId);
        Assert.Equal("SKU-001", entry.ResourceNo);
        Assert.Contains("编辑商品 SKU-001 螺丝", entry.Summary, StringComparison.Ordinal);
        Assert.Contains("\"before\":\"10.00\"", entry.Changes, StringComparison.Ordinal);
        Assert.Contains("\"after\":\"8.80\"", entry.Changes, StringComparison.Ordinal);

        await context.DisposeAsync();
    }

    /// <summary>分类域：新增分类记录创建者与快照名</summary>
    [Fact]
    public async Task CreateCategory_RecordsCategoryCreate()
    {
        var context = TestSupport.CreateDbContext();
        var audit = new RecordingAuditLogger();

        var created = await new CreateCategoryRequestHandler(new CategoryRepository(context), audit)
            .HandleAsync(new CreateCategoryRequest { Name = "紧固件" });

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditResource.Category, entry.Resource);
        Assert.Equal(AuditAction.Create, entry.Action);
        Assert.Equal(created.Id, entry.ResourceId?.ToString());
        Assert.Equal("紧固件", entry.ResourceNo);
        Assert.Contains("新增分类 紧固件", entry.Summary, StringComparison.Ordinal);

        await context.DisposeAsync();
    }

    /// <summary>往来单位域：停用记录状态变更，并保留业务标识快照</summary>
    [Fact]
    public async Task UpdatePartnerStatus_RecordsStatusChange()
    {
        var context = TestSupport.CreateDbContext();
        var partner = TestSupport.NewPartner(name: "供应商甲");
        context.Partners.Add(partner);
        await context.SaveChangesAsync();

        var audit = new RecordingAuditLogger();
        await new UpdatePartnerStatusRequestHandler(
            new PartnerRepository(context),
            new StubCurrentUser(_operatorId),
            audit).HandleAsync(new UpdatePartnerStatusRequest
            {
                Id = partner.Id,
                Status = (int)PartnerStatus.Disabled,
            });

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditResource.Partner, entry.Resource);
        Assert.Equal(AuditAction.StatusChange, entry.Action);
        Assert.Equal("供应商甲", entry.ResourceNo);
        Assert.Contains("停用往来单位 供应商甲", entry.Summary, StringComparison.Ordinal);
        Assert.Contains("\"before\":\"启用\"", entry.Changes, StringComparison.Ordinal);
        Assert.Contains("\"after\":\"停用\"", entry.Changes, StringComparison.Ordinal);

        await context.DisposeAsync();
    }

    // ===== 用户角色域 =====

    /// <summary>用户域：角色变更记录前后角色集合差异</summary>
    [Fact]
    public async Task UpdateUser_RecordsRoleDiff()
    {
        var context = TestSupport.CreateDbContext();
        var warehouseRole = NewRole("仓管员");
        var financeRole = NewRole("财务员");
        var user = TestSupport.NewUser(username: "zhangsan", displayName: "张三");
        context.Roles.AddRange(warehouseRole, financeRole);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        await new UserRoleRepository(context).ReplaceUserRolesAsync(user.Id, [warehouseRole.Id], default);

        var audit = new RecordingAuditLogger();
        await new UpdateUserRequestHandler(
            new UserRepository(context),
            new RoleRepository(context),
            new UserRoleRepository(context),
            new UnitOfWork(context),
            new StubCurrentUser(_operatorId),
            audit).HandleAsync(new UpdateUserRequest
            {
                Id = user.Id,
                DisplayName = "张三",
                RoleIds = [financeRole.Id],
            });

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditResource.User, entry.Resource);
        Assert.Equal(AuditAction.Update, entry.Action);
        Assert.Equal("zhangsan", entry.ResourceNo);
        Assert.Contains("仓管员", entry.Changes, StringComparison.Ordinal);
        Assert.Contains("财务员", entry.Changes, StringComparison.Ordinal);

        await context.DisposeAsync();
    }

    /// <summary>角色域：权限点增减用 + / - 呈现，且不含任何密码 / 令牌字段</summary>
    [Fact]
    public async Task UpdateRole_RecordsPermissionDiff()
    {
        var context = TestSupport.CreateDbContext();
        var role = NewRole("库管");
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        await new RoleRepository(context).ReplacePermissionsAsync(role.Id, [Permissions.ProductsView], default);

        var audit = new RecordingAuditLogger();
        await new UpdateRoleRequestHandler(
            new RoleRepository(context),
            new UserRoleRepository(context),
            new UnitOfWork(context),
            new StubCurrentUser(_operatorId),
            audit).HandleAsync(new UpdateRoleRequest
            {
                Id = role.Id,
                Name = "库管",
                PermissionKeys = [Permissions.ProductsView, Permissions.CategoriesCreate],
            });

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditResource.Role, entry.Resource);
        Assert.Equal(AuditAction.Update, entry.Action);
        Assert.Equal("库管", entry.ResourceNo);
        Assert.Contains("+商品分类.新增", entry.Changes, StringComparison.Ordinal);
        Assert.DoesNotContain("password", entry.Changes ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        await context.DisposeAsync();
    }

    /// <summary>用户域：重置密码只记摘要，任何情况下不落密码相关差异</summary>
    [Fact]
    public async Task ResetPassword_RecordsSummaryWithoutSecrets()
    {
        var context = TestSupport.CreateDbContext();
        var user = TestSupport.NewUser(username: "lisi", displayName: "李四");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var audit = new RecordingAuditLogger();
        await new ResetPasswordRequestHandler(
            new UserRepository(context),
            TestSupport.PasswordHasher,
            new StubCurrentUser(_operatorId),
            audit).HandleAsync(new ResetPasswordRequest { Id = user.Id, NewPassword = "lisi@123" });

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditResource.User, entry.Resource);
        Assert.Equal(AuditAction.Update, entry.Action);
        Assert.Null(entry.Changes);
        Assert.Contains("重置用户密码 李四", entry.Summary, StringComparison.Ordinal);

        await context.DisposeAsync();
    }

    // ===== 单据域 =====

    /// <summary>订单域：关闭采购订单记录 Closed 动作与订单号快照</summary>
    [Fact]
    public async Task ClosePurchaseOrder_RecordsOrderClose()
    {
        // 订单 / 库存 / 流水相关仓储用行为型假实现（InMemory 提供程序不支持 ExecuteUpdateAsync）
        var purchaseOrder = NewPurchaseOrder("PO202609170001", "供应商甲", 1200m);
        var orders = new FakePurchaseOrderRepository();
        orders.Seed(purchaseOrder, []);

        var audit = new RecordingAuditLogger();
        await new ClosePurchaseOrderRequestHandler(orders, new StubCurrentUser(_operatorId), audit)
            .HandleAsync(new ClosePurchaseOrderRequest { Id = purchaseOrder.Id });

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditResource.PurchaseOrder, entry.Resource);
        Assert.Equal(AuditAction.Close, entry.Action);
        Assert.Equal("PO202609170001", entry.ResourceNo);
        Assert.Contains("关闭采购订单 PO202609170001", entry.Summary, StringComparison.Ordinal);
        Assert.Contains("已关闭", entry.Changes, StringComparison.Ordinal);
    }

    /// <summary>采购入库域：作废记录单据号与金额快照</summary>
    [Fact]
    public async Task VoidPurchaseReceipt_RecordsReceiptVoid()
    {
        var calls = new List<string>();
        var (receipt, receipts, inventory) = SeedReceipt(calls, "GR202609170001", "供应商甲", 500m);

        var audit = new RecordingAuditLogger();
        await new VoidPurchaseReceiptRequestHandler(
            receipts,
            new FakePurchaseOrderRepository(calls),
            inventory,
            new FakeStockMovementRepository(calls),
            GeneralLedgerStubs.NewVouchers(),
            GeneralLedgerStubs.NewPeriods(),
            new RecordingUnitOfWork(calls),
            new StubCurrentUser(_operatorId),
            audit).HandleAsync(new VoidPurchaseReceiptRequest { Id = receipt.Id });

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditResource.PurchaseReceipt, entry.Resource);
        Assert.Equal(AuditAction.Void, entry.Action);
        Assert.Equal("GR202609170001", entry.ResourceNo);
        Assert.Contains("作废采购入库单 GR202609170001", entry.Summary, StringComparison.Ordinal);
    }

    /// <summary>收付款域：作废记录收付方向与核销金额快照</summary>
    [Fact]
    public async Task VoidSettlement_RecordsSettlementVoid()
    {
        var calls = new List<string>();
        var (shipment, shipments) = SeedShipment(calls, "GI202609170001", "客户乙", 300m);
        var settlement = new Settlement
        {
            Id = Guid.NewGuid(),
            SettlementNo = "RC202609170001",
            Type = SettlementType.Receipt,
            PartnerId = shipment.PartnerId,
            PartnerName = shipment.PartnerName,
            SettlementDate = DateTimeOffset.UtcNow,
            TotalAmount = 300m,
            Method = SettlementMethod.Cash,
            Status = OrderStatus.Normal,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var settlements = new FakeSettlementRepository(calls);
        settlements.Seed(settlement, [new SettlementItem
        {
            Id = Guid.NewGuid(),
            SettlementId = settlement.Id,
            OrderType = SettlementOrderType.SalesOutbound,
            OrderId = shipment.Id,
            OrderNo = shipment.ShipmentNo,
            OrderDate = shipment.OrderDate,
            OrderTotalAmount = shipment.TotalAmount,
            Amount = 300m,
        }]);

        var audit = new RecordingAuditLogger();
        await new VoidSettlementRequestHandler(
            settlements,
            new FakePurchaseReceiptRepository(calls),
            shipments,
            new FakePurchaseReturnRepository(calls),
            new FakeSalesReturnRepository(calls),
            GeneralLedgerStubs.NewVouchers(),
            GeneralLedgerStubs.NewPeriods(),
            new RecordingUnitOfWork(calls),
            new StubCurrentUser(_operatorId),
            audit).HandleAsync(new VoidSettlementRequest { Id = settlement.Id });

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditResource.Settlement, entry.Resource);
        Assert.Equal(AuditAction.Void, entry.Action);
        Assert.Equal("RC202609170001", entry.ResourceNo);
        Assert.Contains("作废收款单 RC202609170001", entry.Summary, StringComparison.Ordinal);
    }

    /// <summary>盘点域：提交盘点记录行数与差异行数的聚合快照</summary>
    [Fact]
    public async Task CreateStockTake_RecordsItemCountAndProductSnapshot()
    {
        var product = TestSupport.NewProduct(code: "SKU-002", name: "螺母");
        var products = new FakeProductRepository();
        products.ById[product.Id] = product;

        var calls = new List<string>();
        var audit = new RecordingAuditLogger();
        await new CreateStockTakeRequestHandler(
            new FakeStockTakeRepository(calls),
            products,
            new FakeInventoryRepository(calls),
            new FakeStockMovementRepository(calls),
            new RecordingUnitOfWork(calls),
            new StubCurrentUser(_operatorId),
            audit).HandleAsync(new CreateStockTakeRequest
            {
                Type = StockTakeType.Initial,
                TakeDate = DateTimeOffset.UtcNow,
                Items = [new CreateStockTakeItem { ProductId = product.Id, ActualQuantity = 10, UnitCost = 5m }],
            });

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditResource.StockTake, entry.Resource);
        Assert.Equal(AuditAction.Adjust, entry.Action);
        Assert.False(string.IsNullOrWhiteSpace(entry.ResourceNo));
        Assert.Contains("1 行、差异 1 行", entry.Summary, StringComparison.Ordinal);
        Assert.Contains("螺母", entry.Summary, StringComparison.Ordinal);
    }

    /// <summary>成本域：重算无明确业务对象，ResourceId 为空</summary>
    [Fact]
    public async Task RecalculateCosts_RecordsAggregateWithoutResourceId()
    {
        var context = TestSupport.CreateDbContext();

        var audit = new RecordingAuditLogger();
        await new RecalculateCostsRequestHandler(
            new StockMovementRepository(context),
            new InventoryRepository(context),
            new UnitOfWork(context),
            new CostRecalculationLock(),
            audit).HandleAsync(new RecalculateCostsRequest
            {
                Start = DateTimeOffset.UtcNow.AddDays(-1),
                End = DateTimeOffset.UtcNow,
            });

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditResource.Cost, entry.Resource);
        Assert.Equal(AuditAction.Recalculate, entry.Action);
        Assert.Null(entry.ResourceId);
        Assert.Contains("成本重算", entry.Summary, StringComparison.Ordinal);

        await context.DisposeAsync();
    }

    // ===== 4.3 失败路径 =====

    /// <summary>业务规则拒绝（商品不存在）时不产生任何日志</summary>
    [Fact]
    public async Task UpdateProduct_BusinessRejected_RecordsNothing()
    {
        var context = TestSupport.CreateDbContext();
        var audit = new RecordingAuditLogger();
        var handler = new UpdateProductRequestHandler(
            new ProductRepository(context),
            new CategoryRepository(context),
            new InventoryRepository(context),
            new UnitOfWork(context),
            new StubCurrentUser(_operatorId),
            audit);

        var error = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdateProductRequest
        {
            Id = Guid.NewGuid(),
            Name = "不存在",
            CategoryId = Guid.NewGuid(),
            Unit = "个",
            PurchasePrice = 1m,
            SalePrice = 2m,
        }));

        Assert.Equal(ErrorCode.NotFound, error.Code);
        Assert.Empty(audit.Entries);

        await context.DisposeAsync();
    }

    /// <summary>业务规则拒绝（商品编码重复，40101）时不产生任何日志</summary>
    [Fact]
    public async Task CreateProduct_DuplicateCode_RecordsNothing()
    {
        var context = TestSupport.CreateDbContext();
        var category = await SeedCategoryAsync(context);
        context.Products.Add(
            TestSupport.NewProduct(code: "SKU-DUP", name: "已存在商品", categoryId: category.Id));
        await context.SaveChangesAsync();

        var audit = new RecordingAuditLogger();
        var handler = new CreateProductRequestHandler(
            new ProductRepository(context),
            new CategoryRepository(context),
            new InventoryRepository(context),
            new UnitOfWork(context),
            new StubCurrentUser(_operatorId),
            audit);

        var error = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateProductRequest
        {
            Code = "sku-dup",
            Name = "重码商品",
            CategoryId = category.Id,
            Unit = "个",
            PurchasePrice = 1m,
            SalePrice = 2m,
        }));

        Assert.Equal(ErrorCode.ProductCodeExists, error.Code);
        Assert.Empty(audit.Entries);

        await context.DisposeAsync();
    }

    /// <summary>业务规则拒绝（库存不足，40103）时不产生任何日志</summary>
    [Fact]
    public async Task CreateSalesShipment_InsufficientStock_RecordsNothing()
    {
        var context = TestSupport.CreateDbContext();
        var partner = TestSupport.NewPartner(name: "客户丁", type: PartnerType.Customer);
        var product = TestSupport.NewProduct(code: "SKU-SO", name: "线材");
        context.Partners.Add(partner);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var calls = new List<string>();
        var inventory = new FakeInventoryRepository(calls);
        inventory.Seed(product.Id, 5);
        var audit = new RecordingAuditLogger();
        var gl = GeneralLedgerStubs.Create();
        var handler = new CreateSalesShipmentRequestHandler(
            new FakeSalesShipmentRepository(calls),
            new FakeSalesOrderRepository(calls),
            new PartnerRepository(context),
            new ProductRepository(context),
            inventory,
            new FakeStockMovementRepository(calls),
            new FakeSettlementQueryRepository(),
            gl.Vouchers, gl.Mappings, gl.Periods, gl.Accounts,
            new RecordingUnitOfWork(calls),
            new StubCurrentUser(_operatorId),
            audit);

        var error = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateSalesShipmentRequest
        {
            PartnerId = partner.Id,
            OrderDate = DateTimeOffset.UtcNow,
            Items = [new CreateSalesShipmentItem { ProductId = product.Id, Quantity = 10, UnitPrice = 1m }],
        }));

        Assert.Equal(ErrorCode.InsufficientStock, error.Code);
        Assert.Empty(audit.Entries);

        await context.DisposeAsync();
    }

    /// <summary>业务规则拒绝（往来单位类型收窄，40119）时不产生任何日志</summary>
    [Fact]
    public async Task UpdatePartner_TypeNarrowing_RecordsNothing()
    {
        var context = TestSupport.CreateDbContext();
        var partner = TestSupport.NewPartner(name: "往来甲", type: PartnerType.Supplier);
        context.Partners.Add(partner);
        await context.SaveChangesAsync();

        var audit = new RecordingAuditLogger();
        var handler = new UpdatePartnerRequestHandler(
            new PartnerRepository(context),
            new StubCurrentUser(_operatorId),
            audit);

        var error = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdatePartnerRequest
        {
            Id = partner.Id,
            Type = PartnerType.Customer,
        }));

        Assert.Equal(ErrorCode.PartnerTypeNarrowingNotAllowed, error.Code);
        Assert.Empty(audit.Entries);

        await context.DisposeAsync();
    }

    /// <summary>业务规则拒绝（重复作废）时不产生任何日志</summary>
    [Fact]
    public async Task VoidPurchaseReceipt_DuplicateVoid_RecordsNothing()
    {
        var calls = new List<string>();
        var (receipt, receipts, inventory) = SeedReceipt(calls, "GR202609170003", "供应商丙", 100m);
        var audit = new RecordingAuditLogger();
        var handler = new VoidPurchaseReceiptRequestHandler(
            receipts,
            new FakePurchaseOrderRepository(calls),
            inventory,
            new FakeStockMovementRepository(calls),
            GeneralLedgerStubs.NewVouchers(),
            GeneralLedgerStubs.NewPeriods(),
            new RecordingUnitOfWork(calls),
            new StubCurrentUser(_operatorId),
            audit);

        await handler.HandleAsync(new VoidPurchaseReceiptRequest { Id = receipt.Id });
        await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new VoidPurchaseReceiptRequest { Id = receipt.Id }));

        Assert.Single(audit.Entries);
    }

    // ===== 4.4 事务一致性 =====

    /// <summary>
    /// 日志在事务提交前写入：提交失败（回滚）时，消费者感知不到成功，日志也不随失败业务落库
    /// </summary>
    [Fact]
    public async Task UpdateProduct_CommitFailed_LogsWrittenBeforeCommitAndRollback()
    {
        var context = TestSupport.CreateDbContext();
        var category = await SeedCategoryAsync(context);
        var product = TestSupport.NewProduct(code: "SKU-003", name: "螺栓", purchasePrice: 7m, categoryId: category.Id);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var audit = new RecordingAuditLogger();
        var unitOfWork = new ThrowingUnitOfWork();
        var handler = new UpdateProductRequestHandler(
            new ProductRepository(context),
            new CategoryRepository(context),
            new InventoryRepository(context),
            unitOfWork,
            new StubCurrentUser(_operatorId),
            audit);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(new UpdateProductRequest
        {
            Id = product.Id,
            Name = "螺栓",
            CategoryId = category.Id,
            Unit = "个",
            PurchasePrice = 9m,
            SalePrice = 12m,
        }));

        // 日志写入发生在提交之前（与业务同事务）；提交失败 → 回滚，不向调用方暴露半成功状态
        Assert.Single(audit.Entries);
        Assert.Equal(1, unitOfWork.Rollbacks);
        Assert.Empty(context.AuditLogs);

        await context.DisposeAsync();
    }

    // ===== 测试数据 =====

    private static async Task<Category> SeedCategoryAsync(AppDbContext context)
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "紧固件", CreatedAt = DateTimeOffset.UtcNow };
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category;
    }

    private static Role NewRole(string name)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static PurchaseOrder NewPurchaseOrder(string orderNo, string partnerName, decimal totalAmount)
        => new()
        {
            Id = Guid.NewGuid(),
            OrderNo = orderNo,
            PartnerId = Guid.NewGuid(),
            PartnerName = partnerName,
            OrderDate = DateTimeOffset.UtcNow,
            TotalAmount = totalAmount,
            FlowStatus = OrderFlowStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    /// <summary>预置一张可作废的采购入库单（含 1 行明细）与对应库存台账</summary>
    private static (PurchaseReceipt Receipt, FakePurchaseReceiptRepository Receipts, FakeInventoryRepository Inventory)
        SeedReceipt(List<string> calls, string receiptNo, string partnerName, decimal totalAmount)
    {
        var product = TestSupport.NewProduct(code: $"P-{receiptNo}", name: "盘条");
        var receipt = new PurchaseReceipt
        {
            Id = Guid.NewGuid(),
            ReceiptNo = receiptNo,
            PartnerId = Guid.NewGuid(),
            PartnerName = partnerName,
            OrderDate = DateTimeOffset.UtcNow,
            TotalAmount = totalAmount,
            SettledAmount = 0m,
            Status = OrderStatus.Normal,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        var receipts = new FakePurchaseReceiptRepository(calls);
        receipts.Seed(receipt, [new PurchaseReceiptItem
        {
            Id = Guid.NewGuid(),
            ReceiptId = receipt.Id,
            ProductId = product.Id,
            ProductName = product.Name,
            Unit = "个",
            Quantity = 1,
            UnitPrice = totalAmount,
            Subtotal = totalAmount,
        }]);

        var inventory = new FakeInventoryRepository(calls);
        inventory.Seed(product.Id, 1);
        return (receipt, receipts, inventory);
    }

    /// <summary>预置一张可被核销的销售出库单（收付款作废用例的被核销单据）</summary>
    private static (SalesShipment Shipment, FakeSalesShipmentRepository Shipments)
        SeedShipment(List<string> calls, string shipmentNo, string partnerName, decimal totalAmount)
    {
        var shipment = new SalesShipment
        {
            Id = Guid.NewGuid(),
            ShipmentNo = shipmentNo,
            PartnerId = Guid.NewGuid(),
            PartnerName = partnerName,
            OrderDate = DateTimeOffset.UtcNow,
            TotalAmount = totalAmount,
            SettledAmount = totalAmount,
            Status = OrderStatus.Normal,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        var shipments = new FakeSalesShipmentRepository(calls);
        shipments.Seed(shipment, []);
        return (shipment, shipments);
    }
}

/// <summary>提交必然失败的工作单元桩：验证异常外抛且触发回滚</summary>
internal sealed class ThrowingUnitOfWork : IUnitOfWork
{
    /// <summary>回滚次数</summary>
    public int Rollbacks { get; private set; }

    /// <summary>开启事务（桩，不做任何事）</summary>
    public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>提交事务（模拟基础设施失败）</summary>
    public Task CommitAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("提交事务失败");

    /// <summary>回滚事务</summary>
    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        Rollbacks++;
        return Task.CompletedTask;
    }

    /// <summary>异步释放（桩）</summary>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
