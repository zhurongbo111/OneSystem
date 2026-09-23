using App.Core;
using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Quotations.ConvertToOrder;
using App.Core.Features.Quotations.CreateQuotation;
using App.Core.Features.Quotations.GetQuotationById;
using App.Core.Features.Quotations.GetQuotations;
using App.Core.Features.Quotations.UpdateQuotation;
using App.Core.Features.Quotations.VoidQuotation;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 报价单用例测试（specs/037-erp-quotation design.md §6）：
/// 金额重算与单号、明细全量替换、状态流转限制（40166 / 40167）、转单原子（订单 + 报价单回写同一事务、不触碰库存）、
/// 筛选透传与审计写入。
/// </summary>
public class QuotationRequestHandlerTests
{
    private static readonly DateTimeOffset QuotationDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Quotation NewQuotation(QuotationStatus status = QuotationStatus.Draft, Guid partnerId = default)
        => new()
        {
            Id = Guid.NewGuid(),
            QuotationNo = "QT202601010001",
            PartnerId = partnerId,
            PartnerName = "客户",
            QuotationDate = QuotationDate,
            TotalAmount = 10m,
            Status = status,
            CreatedAt = QuotationDate,
            UpdatedAt = QuotationDate,
        };

    private static QuotationItem NewItem(Guid quotationId, int quantity = 10, decimal price = 1m)
        => new()
        {
            Id = SequentialGuidGenerator.NewSequential(),
            QuotationId = quotationId,
            ProductId = Guid.NewGuid(),
            ProductName = "商品",
            Unit = "件",
            Quantity = quantity,
            UnitPrice = price,
            Subtotal = quantity * price,
        };

    private static CreateQuotationRequestHandler NewCreateHandler(
        AppDbContext context, FakeQuotationRepository quotations, StubCurrentUser user, RecordingAuditLogger auditLogger)
        => new(quotations, new PartnerRepository(context), new ProductRepository(context), new RecordingUnitOfWork(), user, auditLogger);

    // ============================== 新增 ==============================

    [Fact]
    public async Task 新增报价单_应重算金额并落库草稿与QT单号()
    {
        using var context = TestSupport.CreateDbContext();
        var partner = TestSupport.NewPartner("甲客户", type: PartnerType.Customer);
        var product = TestSupport.NewProduct("sku-q1", "商品一");
        context.Partners.Add(partner);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var quotations = new FakeQuotationRepository();
        var handler = NewCreateHandler(context, quotations, new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var result = await handler.HandleAsync(new CreateQuotationRequest
        {
            PartnerId = partner.Id,
            QuotationDate = QuotationDate,
            ValidUntil = new DateOnly(2026, 1, 31),
            Items = new[]
            {
                new CreateQuotationItem { ProductId = product.Id, Quantity = 2, UnitPrice = 8.80m },
                new CreateQuotationItem { ProductId = product.Id, Quantity = 3, UnitPrice = 1.00m },
            },
            Remark = "  报价备注  ",
        });

        // 后端重算：小计 = 数量 × 单价、总额 = Σ 小计（不信任前端传值）
        Assert.Equal(2 * 8.80m + 3m, result.TotalAmount);
        Assert.Equal(QuotationStatus.Draft, (QuotationStatus)result.Status);
        Assert.StartsWith("QT", result.QuotationNo);
        Assert.Equal("甲客户", result.PartnerName);
        Assert.Equal((DateOnly?)new DateOnly(2026, 1, 31), result.ValidUntil);
        Assert.Equal("报价备注", result.Remark);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(17.60m, result.Items[0].Subtotal);
        Assert.Equal(new[] { "QT" }, quotations.GeneratedPrefixes);
    }

    [Fact]
    public async Task 新增报价单_单价按前端传入落库_不二次取价()
    {
        using var context = TestSupport.CreateDbContext();
        var partner = TestSupport.NewPartner("甲客户", type: PartnerType.Customer);
        // 商品档案销售价 100，报价单价 8.80：落库取请求值（取价由前端按 036 生效价带出）
        var product = TestSupport.NewProduct("sku-q2", "商品二", purchasePrice: 100m);
        context.Partners.Add(partner);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var quotations = new FakeQuotationRepository();
        var handler = NewCreateHandler(context, quotations, new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var result = await handler.HandleAsync(new CreateQuotationRequest
        {
            PartnerId = partner.Id,
            QuotationDate = QuotationDate,
            Items = new[] { new CreateQuotationItem { ProductId = product.Id, Quantity = 2, UnitPrice = 8.80m } },
        });

        Assert.Equal(8.80m, result.Items[0].UnitPrice);
        Assert.Equal(17.60m, result.Items[0].Subtotal);
        Assert.Equal(17.60m, result.TotalAmount);
    }

    [Fact]
    public async Task 新增报价单_客户不存在_应报40400()
    {
        using var context = TestSupport.CreateDbContext();
        var product = TestSupport.NewProduct("sku-q3");
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var handler = NewCreateHandler(context, new FakeQuotationRepository(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateQuotationRequest
        {
            PartnerId = Guid.NewGuid(),
            QuotationDate = QuotationDate,
            Items = new[] { new CreateQuotationItem { ProductId = product.Id, Quantity = 1, UnitPrice = 1m } },
        }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 新增报价单_商品不存在_应报40400()
    {
        using var context = TestSupport.CreateDbContext();
        var partner = TestSupport.NewPartner("甲客户", type: PartnerType.Customer);
        context.Partners.Add(partner);
        await context.SaveChangesAsync();

        var handler = NewCreateHandler(context, new FakeQuotationRepository(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateQuotationRequest
        {
            PartnerId = partner.Id,
            QuotationDate = QuotationDate,
            Items = new[] { new CreateQuotationItem { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 1m } },
        }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 新增报价单_明细为空_应报OrderItemsEmpty()
    {
        using var context = TestSupport.CreateDbContext();
        var partner = TestSupport.NewPartner("甲客户", type: PartnerType.Customer);
        context.Partners.Add(partner);
        await context.SaveChangesAsync();

        var handler = NewCreateHandler(context, new FakeQuotationRepository(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateQuotationRequest
        {
            PartnerId = partner.Id,
            QuotationDate = QuotationDate,
            Items = Array.Empty<CreateQuotationItem>(),
        }));
        Assert.Equal(ErrorCode.OrderItemsEmpty, ex.Code);
    }

    [Fact]
    public async Task 新增报价单_应写入创建审计日志()
    {
        using var context = TestSupport.CreateDbContext();
        var partner = TestSupport.NewPartner("甲客户", type: PartnerType.Customer);
        var product = TestSupport.NewProduct("sku-q4");
        context.Partners.Add(partner);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var auditLogger = new RecordingAuditLogger();
        var handler = NewCreateHandler(context, new FakeQuotationRepository(), new StubCurrentUser(Guid.NewGuid()), auditLogger);

        var result = await handler.HandleAsync(new CreateQuotationRequest
        {
            PartnerId = partner.Id,
            QuotationDate = QuotationDate,
            Items = new[] { new CreateQuotationItem { ProductId = product.Id, Quantity = 2, UnitPrice = 5m } },
        });

        var entry = Assert.Single(auditLogger.Entries);
        Assert.Equal(AuditResource.Quotation, entry.Resource);
        Assert.Equal(AuditAction.Create, entry.Action);
        Assert.Equal(result.QuotationNo, entry.ResourceNo);
        Assert.Contains("甲客户", entry.Summary, StringComparison.Ordinal);
        Assert.Contains("10.00", entry.Summary, StringComparison.Ordinal);
    }

    // ============================== 编辑 ==============================

    [Fact]
    public async Task 编辑报价单_草稿_应整体替换明细并重算金额()
    {
        using var context = TestSupport.CreateDbContext();
        var partner = TestSupport.NewPartner("甲客户", type: PartnerType.Customer);
        var product = TestSupport.NewProduct("sku-q5", "商品五");
        context.Partners.Add(partner);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var quotations = new FakeQuotationRepository();
        var quotation = NewQuotation(QuotationStatus.Draft, partner.Id);
        quotations.Seed(quotation, new[] { NewItem(quotation.Id, 10) });

        var handler = new UpdateQuotationRequestHandler(
            quotations, new PartnerRepository(context), new ProductRepository(context),
            new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var result = await handler.HandleAsync(new UpdateQuotationRequest
        {
            Id = quotation.Id,
            PartnerId = partner.Id,
            QuotationDate = QuotationDate,
            Items = new[] { new UpdateQuotationItem { ProductId = product.Id, Quantity = 7, UnitPrice = 2m } },
        });

        Assert.Equal(14m, result.TotalAmount);
        Assert.Single(result.Items);
        Assert.Equal(7, result.Items[0].Quantity);
        Assert.Single(quotations.ItemsOf(quotation.Id));
    }

    [Theory]
    [InlineData(QuotationStatus.Converted)]
    [InlineData(QuotationStatus.Voided)]
    public async Task 编辑报价单_非草稿_应报QuotationNotEditable(QuotationStatus status)
    {
        using var context = TestSupport.CreateDbContext();
        var quotations = new FakeQuotationRepository();
        var quotation = NewQuotation(status);
        quotations.Seed(quotation, new[] { NewItem(quotation.Id) });

        var handler = new UpdateQuotationRequestHandler(
            quotations, new PartnerRepository(context), new ProductRepository(context),
            new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdateQuotationRequest
        {
            Id = quotation.Id,
            PartnerId = Guid.NewGuid(),
            QuotationDate = QuotationDate,
            Items = new[] { new UpdateQuotationItem { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 1m } },
        }));
        Assert.Equal(ErrorCode.QuotationNotEditable, ex.Code);
    }

    [Fact]
    public async Task 编辑报价单_不存在_应报40400()
    {
        using var context = TestSupport.CreateDbContext();
        var handler = new UpdateQuotationRequestHandler(
            new FakeQuotationRepository(), new PartnerRepository(context), new ProductRepository(context),
            new RecordingUnitOfWork(), new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdateQuotationRequest
        {
            Id = Guid.NewGuid(),
            PartnerId = Guid.NewGuid(),
            QuotationDate = QuotationDate,
            Items = new[] { new UpdateQuotationItem { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 1m } },
        }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 作废 ==============================

    [Fact]
    public async Task 作废报价单_草稿_应置已作废并记日志()
    {
        using var context = TestSupport.CreateDbContext();
        var quotations = new FakeQuotationRepository();
        var quotation = NewQuotation(QuotationStatus.Draft);
        quotations.Seed(quotation, new[] { NewItem(quotation.Id) });

        var auditLogger = new RecordingAuditLogger();
        var handler = new VoidQuotationRequestHandler(quotations, new StubCurrentUser(Guid.NewGuid()), auditLogger);

        var result = await handler.HandleAsync(new VoidQuotationRequest { Id = quotation.Id });

        Assert.Equal((int)QuotationStatus.Voided, result.Status);
        var statusUpdate = Assert.Single(quotations.StatusUpdates);
        Assert.Equal(QuotationStatus.Voided, statusUpdate.Status);
        Assert.Null(statusUpdate.OrderId);
        Assert.Null(statusUpdate.OrderNo);
        Assert.Equal(AuditAction.Void, Assert.Single(auditLogger.Entries).Action);
    }

    [Theory]
    [InlineData(QuotationStatus.Converted)]
    [InlineData(QuotationStatus.Voided)]
    public async Task 作废报价单_非草稿_应报QuotationNotEditable(QuotationStatus status)
    {
        using var context = TestSupport.CreateDbContext();
        var quotations = new FakeQuotationRepository();
        var quotation = NewQuotation(status);
        quotations.Seed(quotation, new[] { NewItem(quotation.Id) });

        var handler = new VoidQuotationRequestHandler(quotations, new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new VoidQuotationRequest { Id = quotation.Id }));
        Assert.Equal(ErrorCode.QuotationNotEditable, ex.Code);
    }

    // ============================== 转销售订单 ==============================

    [Fact]
    public async Task 转销售订单_草稿_应同一事务创建订单并回写报价单()
    {
        using var context = TestSupport.CreateDbContext();
        var calls = new List<string>();
        var quotation = NewQuotation(QuotationStatus.Draft);
        var quotations = new FakeQuotationRepository(calls);
        quotations.Seed(quotation, new[] { NewItem(quotation.Id, 5, 2m) });

        var salesOrders = new FakeSalesOrderRepository(calls);
        var handler = new ConvertQuotationRequestHandler(
            quotations, salesOrders, new RecordingUnitOfWork(calls),
            new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var result = await handler.HandleAsync(new ConvertQuotationRequest { Id = quotation.Id });

        // 订单：计划数据（待发货）、明细按报价单原样复制、金额一致
        Assert.StartsWith("SO", result.OrderNo);
        var order = Assert.Single(salesOrders.Orders);
        Assert.Equal(quotation.PartnerId, order.PartnerId);
        Assert.Equal(quotation.TotalAmount, order.TotalAmount);
        Assert.Equal(OrderFlowStatus.Pending, order.FlowStatus);
        var orderItems = salesOrders.ItemsOf(order.Id);
        Assert.Single(orderItems);
        Assert.Equal(5, orderItems[0].Quantity);
        Assert.Equal(2m, orderItems[0].UnitPrice);
        Assert.Equal(0, orderItems[0].FulfilledQuantity);

        // 报价单：锁定为已转订单并回写订单 id / 单号
        Assert.Equal((int)QuotationStatus.Converted, (int)quotation.Status);
        Assert.Equal(order.Id, quotation.ConvertedOrderId);
        Assert.Equal(order.OrderNo, quotation.ConvertedOrderNo);

        // 同一事务：Begin → 建订单 → 回写报价单 → Commit（顺序即原子性证据）
        Assert.Equal(new[] { "Begin", "Generate", "Add", "UpdateStatus", "Commit" }, calls);

        // 报价单不产生库存影响：不触碰库存流水与累计执行量回写
        Assert.Empty(salesOrders.FulfilledAdds);
    }

    [Theory]
    [InlineData(QuotationStatus.Converted)]
    [InlineData(QuotationStatus.Voided)]
    public async Task 转销售订单_非草稿_应报QuotationNotConvertible(QuotationStatus status)
    {
        using var context = TestSupport.CreateDbContext();
        var quotations = new FakeQuotationRepository();
        var quotation = NewQuotation(status);
        quotations.Seed(quotation, new[] { NewItem(quotation.Id) });

        var salesOrders = new FakeSalesOrderRepository();
        var handler = new ConvertQuotationRequestHandler(
            quotations, salesOrders, new RecordingUnitOfWork(),
            new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new ConvertQuotationRequest { Id = quotation.Id }));
        Assert.Equal(ErrorCode.QuotationNotConvertible, ex.Code);
        Assert.Empty(salesOrders.Orders);
    }

    [Fact]
    public async Task 转销售订单_不存在_应报40400()
    {
        using var context = TestSupport.CreateDbContext();
        var handler = new ConvertQuotationRequestHandler(
            new FakeQuotationRepository(), new FakeSalesOrderRepository(), new RecordingUnitOfWork(),
            new StubCurrentUser(Guid.NewGuid()), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new ConvertQuotationRequest { Id = Guid.NewGuid() }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 查询 ==============================

    [Fact]
    public async Task 分页查询_应透传筛选并映射明细行数()
    {
        var quotation = NewQuotation();
        var quotations = new FakeQuotationRepository
        {
            PagedResult = ([new QuotationListItem { Quotation = quotation, ItemCount = 3 }], 1),
        };
        var handler = new GetQuotationsRequestHandler(quotations);
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddDays(1);

        var result = await handler.HandleAsync(new GetQuotationsRequest
        {
            Page = 2,
            PageSize = 50,
            Keyword = "QT2026",
            Status = QuotationStatus.Draft,
            Start = start,
            End = end,
        });

        Assert.Equal(("QT2026", QuotationStatus.Draft, (DateTimeOffset?)start, (DateTimeOffset?)end, 2, 50), quotations.LastPagedArgs!.Value);
        Assert.Equal(1, result.Total);
        Assert.Equal(2, result.Page);
        Assert.Equal(50, result.PageSize);
        var row = Assert.Single(result.Items);
        Assert.Equal("QT202601010001", row.QuotationNo);
        Assert.Equal(3, row.ItemCount);
        Assert.Equal((int)QuotationStatus.Draft, row.Status);
    }

    [Fact]
    public async Task 详情_应返回明细与状态()
    {
        var quotations = new FakeQuotationRepository();
        var quotation = NewQuotation(QuotationStatus.Converted);
        quotation.ConvertedOrderId = Guid.NewGuid();
        quotation.ConvertedOrderNo = "SO202601010001";
        quotations.Seed(quotation, new[] { NewItem(quotation.Id, 2, 3m) });

        var handler = new GetQuotationByIdRequestHandler(quotations);
        var result = await handler.HandleAsync(new GetQuotationByIdRequest { Id = quotation.Id });

        Assert.Equal((int)QuotationStatus.Converted, result.Status);
        Assert.Equal("SO202601010001", result.ConvertedOrderNo);
        Assert.Equal(quotation.ConvertedOrderId.ToString(), result.ConvertedOrderId);
        Assert.Equal(6m, Assert.Single(result.Items).Subtotal);
    }

    [Fact]
    public async Task 详情_不存在_应报40400()
    {
        var handler = new GetQuotationByIdRequestHandler(new FakeQuotationRepository());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new GetQuotationByIdRequest { Id = Guid.NewGuid() }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }
}
