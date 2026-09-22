using App.Core;
using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Invoices.CreateInvoice;
using App.Core.Features.Invoices.GetInvoiceById;
using App.Core.Features.Invoices.GetInvoices;
using App.Core.Features.Invoices.GetInvoicableOrders;
using App.Core.Features.Invoices.VoidInvoice;
using App.Infrastructure;
using App.Infrastructure.Persistence;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// 发票用例测试（design.md §6）：登记（税额 / 合计后端重算、快照、税额舍入、审计；异常 40110 / 40132 / 40400 / 40108 /
/// 40109 / 40104 / 40134 / 40135 / 40133 且失败路径无写入 + Rollback）、作废（只改状态、不触碰单据仓储；40104 / 40400）、
/// 列表（关键词含关联单据号 / 分页 / 摘要）、可开票候选（方向映射与未开票过滤）、未开票金额恒等链路。
/// 发票 / 往来 / 跨表查询用真实仓储 + InMemory（写路径为普通 SaveChanges），单据与工作单元用行为型假实现。
/// </summary>
public class InvoiceRequestHandlerTests
{
    private static readonly DateTimeOffset InvoiceDate = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset OrderDate = new(2025, 12, 20, 0, 0, 0, TimeSpan.Zero);

    private sealed record Harness(
        AppDbContext Context,
        Partner Partner,
        FakePurchaseReceiptRepository PurchaseReceipts,
        FakeSalesShipmentRepository SalesShipments,
        FakePurchaseReturnRepository PurchaseReturns,
        FakeSalesReturnRepository SalesReturns,
        RecordingUnitOfWork Uow,
        StubCurrentUser User,
        List<string> Calls)
    {
        internal InvoiceRepository Invoices => new(Context);

        internal InvoiceQueryRepository Queries => new(Context);
    }

    private static async Task<Harness> CreateHarnessAsync(
        PartnerType partnerType = PartnerType.Customer,
        PartnerStatus partnerStatus = PartnerStatus.Enabled)
    {
        var context = TestSupport.CreateDbContext();
        var partner = TestSupport.NewPartner("往来一", partnerType, partnerStatus);
        context.Partners.Add(partner);
        await context.SaveChangesAsync();

        var calls = new List<string>();
        return new Harness(
            context,
            partner,
            new FakePurchaseReceiptRepository(calls),
            new FakeSalesShipmentRepository(calls),
            new FakePurchaseReturnRepository(calls),
            new FakeSalesReturnRepository(calls),
            new RecordingUnitOfWork(calls),
            new StubCurrentUser(Guid.NewGuid()),
            calls);
    }

    private static CreateInvoiceRequestHandler CreateCreateHandler(Harness harness, IInvoiceRepository? invoices = null)
        => new(
            invoices ?? harness.Invoices,
            harness.Queries,
            harness.PurchaseReceipts,
            harness.SalesShipments,
            harness.PurchaseReturns,
            harness.SalesReturns,
            new PartnerRepository(harness.Context),
            harness.Uow,
            harness.User,
            TestSupport.AuditLogger);

    private static VoidInvoiceRequestHandler CreateVoidHandler(Harness harness)
        => new(harness.Invoices, harness.Uow, harness.User, TestSupport.AuditLogger);

    private static InvoiceQueryRepository InvoiceQuery(Harness harness) => new(harness.Context);

    private static CreateInvoiceItem Line(SettlementOrderType orderType, Guid orderId, decimal amount)
        => new() { OrderType = orderType, OrderId = orderId, Amount = amount };

    private static SalesShipment NewSalesShipment(Guid partnerId, string no = "GI202512200001", decimal totalAmount = 1000m, OrderStatus status = OrderStatus.Normal)
    {
        var now = DateTimeOffset.UtcNow;
        return new SalesShipment
        {
            Id = Guid.NewGuid(),
            ShipmentNo = no,
            PartnerId = partnerId,
            PartnerName = "往来一",
            OrderDate = OrderDate,
            TotalAmount = totalAmount,
            SettledAmount = 0m,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static PurchaseReceipt NewPurchaseReceipt(Guid partnerId, string no = "GR202512200001", decimal totalAmount = 1000m, OrderStatus status = OrderStatus.Normal)
    {
        var now = DateTimeOffset.UtcNow;
        return new PurchaseReceipt
        {
            Id = Guid.NewGuid(),
            ReceiptNo = no,
            PartnerId = partnerId,
            PartnerName = "往来一",
            OrderDate = OrderDate,
            TotalAmount = totalAmount,
            SettledAmount = 0m,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>直接落库一张发票（模拟「此前已开票」的既有数据，用于未开票金额与筛选用例）</summary>
    private static async Task<Invoice> SeedInvoiceAsync(
        AppDbContext context,
        Guid partnerId,
        string invoiceNo,
        SettlementOrderType orderType,
        Guid orderId,
        string orderNo,
        decimal lineAmount,
        decimal amountExcludingTax,
        decimal taxRate,
        InvoiceType type = InvoiceType.Sales,
        OrderStatus status = OrderStatus.Normal,
        DateTimeOffset? invoiceDate = null)
    {
        var now = DateTimeOffset.UtcNow;
        var taxAmount = Math.Round(amountExcludingTax * taxRate, 2, MidpointRounding.AwayFromZero);
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNo = invoiceNo,
            Type = type,
            PartnerId = partnerId,
            PartnerName = "往来一",
            InvoiceDate = invoiceDate ?? InvoiceDate,
            AmountExcludingTax = amountExcludingTax,
            TaxRate = taxRate,
            TaxAmount = taxAmount,
            TotalAmount = amountExcludingTax + taxAmount,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
        };
        context.Invoices.Add(invoice);
        context.InvoiceItems.Add(new InvoiceItem
        {
            Id = SequentialGuidGenerator.NewSequential(),
            InvoiceId = invoice.Id,
            OrderType = orderType,
            OrderId = orderId,
            OrderNo = orderNo,
            OrderDate = OrderDate,
            OrderTotalAmount = 1000m,
            Amount = lineAmount,
        });
        await context.SaveChangesAsync();
        return invoice;
    }

    // ============================== 登记：正常分支 ==============================

    [Fact]
    public async Task CreateInvoice_销项合法请求_应重算税额合计并落库快照与审计()
    {
        var harness = await CreateHarnessAsync();
        await using var _ = harness.Context;
        var shipment = NewSalesShipment(harness.Partner.Id);
        harness.SalesShipments.Seed(shipment, []);

        var result = await CreateCreateHandler(harness).HandleAsync(new CreateInvoiceRequest
        {
            InvoiceNo = " 12345678 ",
            Type = InvoiceType.Sales,
            PartnerId = harness.Partner.Id,
            InvoiceDate = InvoiceDate,
            AmountExcludingTax = 900m,
            TaxRate = 0.13m,
            Items = [Line(SettlementOrderType.SalesOutbound, shipment.Id, 900m)],
            Remark = "  备注  ",
        });

        // 税额与价税合计由后端重算（请求里没有这两个字段，前端传值无法影响）
        var saved = await harness.Context.Invoices.SingleAsync();
        Assert.Equal("12345678", saved.InvoiceNo);
        Assert.Equal(117.00m, saved.TaxAmount);
        Assert.Equal(1017.00m, saved.TotalAmount);
        Assert.Equal("备注", saved.Remark);
        Assert.Equal(harness.User.UserId, saved.CreatedBy);
        Assert.Equal(OrderStatus.Normal, saved.Status);

        // 明细快照：单据号 / 日期 / 总额来自被关联单据；明细顺序与请求一致
        var item = await harness.Context.InvoiceItems.SingleAsync();
        Assert.Equal(shipment.Id, item.OrderId);
        Assert.Equal(SettlementOrderType.SalesOutbound, item.OrderType);
        Assert.Equal(shipment.ShipmentNo, item.OrderNo);
        Assert.Equal(OrderDate, item.OrderDate);
        Assert.Equal(1000m, item.OrderTotalAmount);
        Assert.Equal(900m, item.Amount);

        Assert.Equal(saved.Id.ToString(), result.Id);
        Assert.Equal(1017.00m, result.TotalAmount);
        Assert.Single(result.Items);
        Assert.Contains("Commit", harness.Calls);
    }

    [Theory]
    [InlineData(900, 0.13, 117.00)]
    [InlineData(33.33, 0.13, 4.33)]
    [InlineData(0.10, 0.05, 0.01)]
    [InlineData(500, 0, 0)]
    public async Task CreateInvoice_税额应四舍五入到分(decimal amountExcludingTax, decimal taxRate, decimal expectedTax)
    {
        var harness = await CreateHarnessAsync();
        await using var _ = harness.Context;
        var shipment = NewSalesShipment(harness.Partner.Id);
        harness.SalesShipments.Seed(shipment, []);

        var result = await CreateCreateHandler(harness).HandleAsync(new CreateInvoiceRequest
        {
            InvoiceNo = $"INV{expectedTax}",
            Type = InvoiceType.Sales,
            PartnerId = harness.Partner.Id,
            InvoiceDate = InvoiceDate,
            AmountExcludingTax = amountExcludingTax,
            TaxRate = taxRate,
            Items = [Line(SettlementOrderType.SalesOutbound, shipment.Id, 100m)],
        });

        Assert.Equal(expectedTax, result.TaxAmount);
        Assert.Equal(amountExcludingTax + expectedTax, result.TotalAmount);
    }

    [Fact]
    public async Task CreateInvoice_进项合法请求_应关联采购入库单()
    {
        var harness = await CreateHarnessAsync(PartnerType.Supplier);
        await using var _ = harness.Context;
        var receipt = NewPurchaseReceipt(harness.Partner.Id);
        harness.PurchaseReceipts.Seed(receipt, []);

        var result = await CreateCreateHandler(harness).HandleAsync(new CreateInvoiceRequest
        {
            InvoiceNo = "GR-INV-1",
            Type = InvoiceType.Purchase,
            PartnerId = harness.Partner.Id,
            InvoiceDate = InvoiceDate,
            AmountExcludingTax = 1000m,
            TaxRate = 0.09m,
            Items = [Line(SettlementOrderType.PurchaseInbound, receipt.Id, 1000m)],
        });

        Assert.Equal((int)InvoiceType.Purchase, result.Type);
        Assert.Equal(90.00m, result.TaxAmount);
        Assert.Equal(SettlementOrderType.PurchaseInbound, (SettlementOrderType)result.Items[0].OrderType);
    }

    // ============================== 登记：异常分支 ==============================

    [Fact]
    public async Task CreateInvoice_关联明细为空_应抛业务异常40110()
    {
        var harness = await CreateHarnessAsync();
        await using var _ = harness.Context;

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(harness).HandleAsync(new CreateInvoiceRequest
            {
                InvoiceNo = "INV-1",
                Type = InvoiceType.Sales,
                PartnerId = harness.Partner.Id,
                InvoiceDate = InvoiceDate,
                AmountExcludingTax = 100m,
                TaxRate = 0.13m,
                Items = [],
            }));

        Assert.Equal(ErrorCode.OrderItemsEmpty, ex.Code);
        Assert.Empty(await harness.Context.Invoices.ToListAsync());
    }

    [Fact]
    public async Task CreateInvoice_发票号重复_应抛业务异常40132()
    {
        var harness = await CreateHarnessAsync();
        await using var _ = harness.Context;
        var shipment = NewSalesShipment(harness.Partner.Id);
        harness.SalesShipments.Seed(shipment, []);
        await SeedInvoiceAsync(harness.Context, harness.Partner.Id, "INV-1", SettlementOrderType.SalesOutbound, shipment.Id, shipment.ShipmentNo, 100m, 100m, 0.13m);

        // 大小写不敏感：inv-1 与 INV-1 视为重复
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(harness).HandleAsync(new CreateInvoiceRequest
            {
                InvoiceNo = "inv-1",
                Type = InvoiceType.Sales,
                PartnerId = harness.Partner.Id,
                InvoiceDate = InvoiceDate,
                AmountExcludingTax = 100m,
                TaxRate = 0.13m,
                Items = [Line(SettlementOrderType.SalesOutbound, shipment.Id, 100m)],
            }));

        Assert.Equal(ErrorCode.InvoiceNoExists, ex.Code);
        Assert.Equal(1, await harness.Context.Invoices.CountAsync());
    }

    [Fact]
    public async Task CreateInvoice_往来单位不存在_应抛业务异常40400()
    {
        var harness = await CreateHarnessAsync();
        await using var _ = harness.Context;

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(harness).HandleAsync(new CreateInvoiceRequest
            {
                InvoiceNo = "INV-1",
                Type = InvoiceType.Sales,
                PartnerId = Guid.NewGuid(),
                InvoiceDate = InvoiceDate,
                AmountExcludingTax = 100m,
                TaxRate = 0.13m,
                Items = [Line(SettlementOrderType.SalesOutbound, Guid.NewGuid(), 100m)],
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task CreateInvoice_往来单位停用_应抛业务异常40108()
    {
        var harness = await CreateHarnessAsync(partnerStatus: PartnerStatus.Disabled);
        await using var _ = harness.Context;

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(harness).HandleAsync(new CreateInvoiceRequest
            {
                InvoiceNo = "INV-1",
                Type = InvoiceType.Sales,
                PartnerId = harness.Partner.Id,
                InvoiceDate = InvoiceDate,
                AmountExcludingTax = 100m,
                TaxRate = 0.13m,
                Items = [Line(SettlementOrderType.SalesOutbound, Guid.NewGuid(), 100m)],
            }));

        Assert.Equal(ErrorCode.PartnerDisabled, ex.Code);
    }

    [Fact]
    public async Task CreateInvoice_往来类型与发票方向不匹配_应抛业务异常40109()
    {
        // 进项发票却选了客户
        var harness = await CreateHarnessAsync(PartnerType.Customer);
        await using var _ = harness.Context;

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(harness).HandleAsync(new CreateInvoiceRequest
            {
                InvoiceNo = "INV-1",
                Type = InvoiceType.Purchase,
                PartnerId = harness.Partner.Id,
                InvoiceDate = InvoiceDate,
                AmountExcludingTax = 100m,
                TaxRate = 0.13m,
                Items = [Line(SettlementOrderType.PurchaseInbound, Guid.NewGuid(), 100m)],
            }));

        Assert.Equal(ErrorCode.PartnerTypeMismatch, ex.Code);
    }

    [Fact]
    public async Task CreateInvoice_被关联单据不存在_应抛业务异常40400()
    {
        var harness = await CreateHarnessAsync();
        await using var _ = harness.Context;

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(harness).HandleAsync(new CreateInvoiceRequest
            {
                InvoiceNo = "INV-1",
                Type = InvoiceType.Sales,
                PartnerId = harness.Partner.Id,
                InvoiceDate = InvoiceDate,
                AmountExcludingTax = 100m,
                TaxRate = 0.13m,
                Items = [Line(SettlementOrderType.SalesOutbound, Guid.NewGuid(), 100m)],
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
        // 只取主表字段：不查明细分片
        Assert.Equal([false], harness.SalesShipments.DetailQueries.Select(q => q.IncludeItems).ToList());
    }

    [Fact]
    public async Task CreateInvoice_被关联单据已作废_应抛业务异常40104()
    {
        var harness = await CreateHarnessAsync();
        await using var _ = harness.Context;
        var shipment = NewSalesShipment(harness.Partner.Id, status: OrderStatus.Voided);
        harness.SalesShipments.Seed(shipment, []);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(harness).HandleAsync(new CreateInvoiceRequest
            {
                InvoiceNo = "INV-1",
                Type = InvoiceType.Sales,
                PartnerId = harness.Partner.Id,
                InvoiceDate = InvoiceDate,
                AmountExcludingTax = 100m,
                TaxRate = 0.13m,
                Items = [Line(SettlementOrderType.SalesOutbound, shipment.Id, 100m)],
            }));

        Assert.Equal(ErrorCode.OrderVoided, ex.Code);
    }

    [Fact]
    public async Task CreateInvoice_方向不匹配_应抛业务异常40134()
    {
        var harness = await CreateHarnessAsync();
        await using var _ = harness.Context;
        var receipt = NewPurchaseReceipt(harness.Partner.Id);
        harness.PurchaseReceipts.Seed(receipt, []);

        // 销项票关联采购入库单
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(harness).HandleAsync(new CreateInvoiceRequest
            {
                InvoiceNo = "INV-1",
                Type = InvoiceType.Sales,
                PartnerId = harness.Partner.Id,
                InvoiceDate = InvoiceDate,
                AmountExcludingTax = 100m,
                TaxRate = 0.13m,
                Items = [Line(SettlementOrderType.PurchaseInbound, receipt.Id, 100m)],
            }));

        Assert.Equal(ErrorCode.InvoiceDirectionMismatch, ex.Code);
    }

    [Fact]
    public async Task CreateInvoice_往来不一致_应抛业务异常40135()
    {
        var harness = await CreateHarnessAsync();
        await using var _ = harness.Context;
        var shipment = NewSalesShipment(Guid.NewGuid());
        harness.SalesShipments.Seed(shipment, []);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(harness).HandleAsync(new CreateInvoiceRequest
            {
                InvoiceNo = "INV-1",
                Type = InvoiceType.Sales,
                PartnerId = harness.Partner.Id,
                InvoiceDate = InvoiceDate,
                AmountExcludingTax = 100m,
                TaxRate = 0.13m,
                Items = [Line(SettlementOrderType.SalesOutbound, shipment.Id, 100m)],
            }));

        Assert.Equal(ErrorCode.InvoicePartnerMismatch, ex.Code);
    }

    [Fact]
    public async Task CreateInvoice_超过未开票金额_应抛业务异常40133且失败路径无写入()
    {
        var harness = await CreateHarnessAsync();
        await using var _ = harness.Context;
        var shipment = NewSalesShipment(harness.Partner.Id);
        harness.SalesShipments.Seed(shipment, []);
        // 此前已开票 900 → 未开票金额 100
        await SeedInvoiceAsync(harness.Context, harness.Partner.Id, "INV-0", SettlementOrderType.SalesOutbound, shipment.Id, shipment.ShipmentNo, 900m, 900m, 0.13m);
        var callsBefore = harness.Calls.Count;

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(harness).HandleAsync(new CreateInvoiceRequest
            {
                InvoiceNo = "INV-1",
                Type = InvoiceType.Sales,
                PartnerId = harness.Partner.Id,
                InvoiceDate = InvoiceDate,
                AmountExcludingTax = 150m,
                TaxRate = 0.13m,
                Items = [Line(SettlementOrderType.SalesOutbound, shipment.Id, 150m)],
            }));

        Assert.Equal(ErrorCode.InvoiceAmountExceeded, ex.Code);
        Assert.Contains(shipment.ShipmentNo, ex.Message);
        Assert.Contains("100.00", ex.Message);

        // 失败路径：无任何写入（仍只有此前那 1 张）；查库约束在事务开启前判完，故不进入事务
        Assert.Equal(1, await harness.Context.Invoices.CountAsync());
        var calls = harness.Calls.Skip(callsBefore).ToList();
        Assert.DoesNotContain("Begin", calls);
        Assert.DoesNotContain("Commit", calls);
    }

    [Fact]
    public async Task CreateInvoice_事务内写入失败_应回滚且不留痕()
    {
        var harness = await CreateHarnessAsync();
        await using var _ = harness.Context;
        var shipment = NewSalesShipment(harness.Partner.Id);
        harness.SalesShipments.Seed(shipment, []);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateCreateHandler(harness, new FailingAddInvoiceRepository(harness.Context)).HandleAsync(new CreateInvoiceRequest
            {
                InvoiceNo = "INV-1",
                Type = InvoiceType.Sales,
                PartnerId = harness.Partner.Id,
                InvoiceDate = InvoiceDate,
                AmountExcludingTax = 900m,
                TaxRate = 0.13m,
                Items = [Line(SettlementOrderType.SalesOutbound, shipment.Id, 900m)],
            }));

        Assert.Equal("模拟写入失败", ex.Message);
        Assert.Empty(await harness.Context.Invoices.ToListAsync());
        Assert.Contains("Begin", harness.Calls);
        Assert.Contains("Rollback", harness.Calls);
        Assert.DoesNotContain("Commit", harness.Calls);
    }

    // ============================== 作废 ==============================

    [Fact]
    public async Task VoidInvoice_应置作废并写审计_且不触碰任何单据仓储()
    {
        var harness = await CreateHarnessAsync();
        await using var _ = harness.Context;
        var shipment = NewSalesShipment(harness.Partner.Id);
        harness.SalesShipments.Seed(shipment, []);
        var invoice = await SeedInvoiceAsync(harness.Context, harness.Partner.Id, "INV-1", SettlementOrderType.SalesOutbound, shipment.Id, shipment.ShipmentNo, 900m, 900m, 0.13m);

        var result = await CreateVoidHandler(harness).HandleAsync(new VoidInvoiceRequest { Id = invoice.Id });

        Assert.Equal((int)OrderStatus.Voided, result.Status);
        Assert.Equal(OrderStatus.Voided, (await harness.Context.Invoices.SingleAsync()).Status);
        Assert.Contains("Commit", harness.Calls);

        // 只改主表状态：不触碰任何单据仓储（未开票金额按聚合自动释放）
        Assert.Empty(harness.SalesShipments.DetailQueries);
        Assert.Empty(harness.PurchaseReceipts.DetailQueries);
        Assert.DoesNotContain("AddSettledAmount", harness.Calls);
    }

    [Fact]
    public async Task VoidInvoice_已作废_应抛业务异常40104()
    {
        var harness = await CreateHarnessAsync();
        await using var _ = harness.Context;
        var invoice = await SeedInvoiceAsync(harness.Context, harness.Partner.Id, "INV-1", SettlementOrderType.SalesOutbound, Guid.NewGuid(), "GI001", 100m, 100m, 0.13m, status: OrderStatus.Voided);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateVoidHandler(harness).HandleAsync(new VoidInvoiceRequest { Id = invoice.Id }));

        Assert.Equal(ErrorCode.OrderVoided, ex.Code);
    }

    [Fact]
    public async Task VoidInvoice_不存在_应抛业务异常40400()
    {
        var harness = await CreateHarnessAsync();
        await using var _ = harness.Context;

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateVoidHandler(harness).HandleAsync(new VoidInvoiceRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 列表与详情 ==============================

    [Fact]
    public async Task GetInvoices_关键词命中关联单据号_应返回摘要与分页()
    {
        var harness = await CreateHarnessAsync();
        await using var _ = harness.Context;
        var shipment = NewSalesShipment(harness.Partner.Id);
        harness.SalesShipments.Seed(shipment, []);
        await SeedInvoiceAsync(harness.Context, harness.Partner.Id, "INV-1", SettlementOrderType.SalesOutbound, shipment.Id, shipment.ShipmentNo, 900m, 900m, 0.13m);
        await SeedInvoiceAsync(harness.Context, harness.Partner.Id, "INV-2", SettlementOrderType.SalesOutbound, Guid.NewGuid(), "GI999", 100m, 100m, 0.13m);

        var handler = new GetInvoicesRequestHandler(new InvoiceRepository(harness.Context));

        // 关键词命中关联单据号（明细 EXISTS 子查询）
        var byOrderNo = await handler.HandleAsync(new GetInvoicesRequest { Keyword = shipment.ShipmentNo, Page = 1, PageSize = 20 });
        Assert.Equal(1, byOrderNo.Total);
        Assert.Equal("INV-1", byOrderNo.Items[0].InvoiceNo);
        Assert.Equal(shipment.ShipmentNo, byOrderNo.Items[0].OrderNoSummary);
        Assert.Equal(0.13m, byOrderNo.Items[0].TaxRate);

        // 关键词命中往来名称 → 两张都在
        var byPartner = await handler.HandleAsync(new GetInvoicesRequest { Keyword = "往来一", Page = 1, PageSize = 20 });
        Assert.Equal(2, byPartner.Total);

        // 类型筛选
        var byType = await handler.HandleAsync(new GetInvoicesRequest { Type = InvoiceType.Purchase, Page = 1, PageSize = 20 });
        Assert.Equal(0, byType.Total);

        // 分页
        var paged = await handler.HandleAsync(new GetInvoicesRequest { Page = 2, PageSize = 1 });
        Assert.Equal(2, paged.Total);
        Assert.Single(paged.Items);
        Assert.Equal(2, paged.Page);
    }

    [Fact]
    public async Task GetInvoiceById_不存在_应抛业务异常40400()
    {
        var harness = await CreateHarnessAsync();
        await using var _ = harness.Context;

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => new GetInvoiceByIdRequestHandler(new InvoiceRepository(harness.Context))
                .HandleAsync(new GetInvoiceByIdRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 可开票候选 ==============================

    [Fact]
    public async Task GetInvoicableOrders_应按方向映射单据类型_且仅返回未开票金额大于0的单据()
    {
        var harness = await CreateHarnessAsync(PartnerType.Both);
        await using var _ = harness.Context;
        var shipment = NewSalesShipment(harness.Partner.Id, "GI202512200001", 1000m);
        var otherShipment = NewSalesShipment(harness.Partner.Id, "GI202512200002", 500m);
        var receipt = NewPurchaseReceipt(harness.Partner.Id, "GR202512200001", 800m);
        var receiptOfOtherPartner = NewPurchaseReceipt(Guid.NewGuid(), "GR202512200009", 300m);
        harness.Context.SalesShipments.AddRange(shipment, otherShipment);
        harness.Context.PurchaseReceipts.AddRange(receipt, receiptOfOtherPartner);
        await harness.Context.SaveChangesAsync();

        // 销项单已开满 → 不出现在候选里
        await SeedInvoiceAsync(harness.Context, harness.Partner.Id, "INV-FULL", SettlementOrderType.SalesOutbound, otherShipment.Id, otherShipment.ShipmentNo, 500m, 500m, 0.13m);

        var handler = new GetInvoicableOrdersRequestHandler(InvoiceQuery(harness));

        // 销项方向：仅销售出库单（采购入库单不出现），且已开满的不出现
        var sales = await handler.HandleAsync(new GetInvoicableOrdersRequest { PartnerId = harness.Partner.Id, Type = InvoiceType.Sales, Page = 1, PageSize = 20 });
        Assert.Equal(1, sales.Total);
        Assert.Equal(shipment.ShipmentNo, sales.Items[0].OrderNo);
        Assert.Equal((int)SettlementOrderType.SalesOutbound, sales.Items[0].OrderType);
        Assert.Equal(1000m, sales.Items[0].UninvoicedAmount);

        // 进项方向：仅采购入库单（他人的单据 / 已作废的不出现）
        var purchase = await handler.HandleAsync(new GetInvoicableOrdersRequest { PartnerId = harness.Partner.Id, Type = InvoiceType.Purchase, Page = 1, PageSize = 20 });
        Assert.Equal(1, purchase.Total);
        Assert.Equal(receipt.ReceiptNo, purchase.Items[0].OrderNo);
        Assert.Equal(800m, purchase.Items[0].TotalAmount);
        Assert.Equal(0m, purchase.Items[0].InvoicedAmount);

        // 部分开票后未开票金额随之下降
        await SeedInvoiceAsync(harness.Context, harness.Partner.Id, "INV-PART", SettlementOrderType.SalesOutbound, shipment.Id, shipment.ShipmentNo, 400m, 400m, 0.13m);
        var afterPartial = await handler.HandleAsync(new GetInvoicableOrdersRequest { PartnerId = harness.Partner.Id, Type = InvoiceType.Sales, Page = 1, PageSize = 20 });
        Assert.Equal(1, afterPartial.Total);
        Assert.Equal(400m, afterPartial.Items[0].InvoicedAmount);
        Assert.Equal(600m, afterPartial.Items[0].UninvoicedAmount);
    }

    // ============================== 未开票金额恒等链路 ==============================

    [Fact]
    public async Task 未开票金额_开票900_再开100_作废首张后应回到900()
    {
        var harness = await CreateHarnessAsync();
        await using var _ = harness.Context;
        var shipment = NewSalesShipment(harness.Partner.Id);
        harness.SalesShipments.Seed(shipment, []);
        var orderType = SettlementOrderType.SalesOutbound;
        var queries = InvoiceQuery(harness);

        Assert.Equal(1000m, 1000m - await queries.GetInvoicedAmountAsync(orderType, shipment.Id));

        var first = await CreateCreateHandler(harness).HandleAsync(new CreateInvoiceRequest
        {
            InvoiceNo = "INV-900",
            Type = InvoiceType.Sales,
            PartnerId = harness.Partner.Id,
            InvoiceDate = InvoiceDate,
            AmountExcludingTax = 900m,
            TaxRate = 0.13m,
            Items = [Line(orderType, shipment.Id, 900m)],
        });
        Assert.Equal(100m, 1000m - await queries.GetInvoicedAmountAsync(orderType, shipment.Id));

        await CreateCreateHandler(harness).HandleAsync(new CreateInvoiceRequest
        {
            InvoiceNo = "INV-100",
            Type = InvoiceType.Sales,
            PartnerId = harness.Partner.Id,
            InvoiceDate = InvoiceDate,
            AmountExcludingTax = 100m,
            TaxRate = 0.13m,
            Items = [Line(orderType, shipment.Id, 100m)],
        });
        Assert.Equal(0m, 1000m - await queries.GetInvoicedAmountAsync(orderType, shipment.Id));

        await CreateVoidHandler(harness).HandleAsync(new VoidInvoiceRequest { Id = Guid.Parse(first.Id) });
        Assert.Equal(900m, 1000m - await queries.GetInvoicedAmountAsync(orderType, shipment.Id));
    }

    /// <summary>
    /// 发票仓储桩：仅把 <c>AddAsync</c> 置为失败（其余委托真实仓储），用于覆盖事务内写入失败 → 回滚分支
    /// </summary>
    private sealed class FailingAddInvoiceRepository : IInvoiceRepository
    {
        private readonly InvoiceRepository _inner;

        public FailingAddInvoiceRepository(AppDbContext context)
        {
            _inner = new InvoiceRepository(context);
        }

        public Task<(IReadOnlyList<InvoiceListItem> Items, int Total)> GetPagedAsync(
            string? keyword,
            InvoiceType? type,
            Guid? partnerId,
            DateTimeOffset? start,
            DateTimeOffset? end,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
            => _inner.GetPagedAsync(keyword, type, partnerId, start, end, page, pageSize, cancellationToken);

        public Task<(Invoice? Invoice, IReadOnlyList<InvoiceItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
            => _inner.GetDetailAsync(id, cancellationToken);

        public Task<bool> ExistsByInvoiceNoAsync(string invoiceNo, CancellationToken cancellationToken = default)
            => _inner.ExistsByInvoiceNoAsync(invoiceNo, cancellationToken);

        public Task AddAsync(Invoice invoice, IReadOnlyList<InvoiceItem> items, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("模拟写入失败");

        public Task UpdateStatusAsync(Guid id, OrderStatus status, Guid? operatorId, CancellationToken cancellationToken = default)
            => _inner.UpdateStatusAsync(id, status, operatorId, cancellationToken);

        public Task<IReadOnlyList<InvoiceItem>> GetItemsByInvoiceIdsAsync(IReadOnlyCollection<Guid> invoiceIds, CancellationToken cancellationToken = default)
            => _inner.GetItemsByInvoiceIdsAsync(invoiceIds, cancellationToken);
    }
}