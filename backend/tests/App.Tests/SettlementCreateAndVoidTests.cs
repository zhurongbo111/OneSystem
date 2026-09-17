using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Settlements.CreateSettlement;
using App.Core.Features.Settlements.VoidSettlement;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 收付款单创建 / 作废用例测试（design.md §6）：
/// CreateSettlement（四种核销方向成功 + 单号前缀 / 总额重算 / 逐行累加 / 明细快照；异常 40110 / 40112 / 40113 / 40114 /
/// 40400 / 40104 / 40108 / 40109 且失败路径无累加 + Rollback）；
/// VoidSettlement（逐行回退 + 置作废；已作废 40104；不存在 40400）。
/// 单据 / 收付款 / 工作单元用行为型假实现（规避 InMemory 不支持 ExecuteUpdateAsync），往来单位用真实仓储 + InMemory。
/// </summary>
public class SettlementCreateAndVoidTests
{
    private static readonly DateTimeOffset SettlementDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset OrderDate = new(2025, 12, 20, 0, 0, 0, TimeSpan.Zero);

    private sealed record Harness(
        AppDbContext Context,
        Partner Partner,
        FakeSettlementRepository Settlements,
        FakePurchaseOrderRepository PurchaseOrders,
        FakeSalesOrderRepository SalesOrders,
        FakePurchaseReturnRepository PurchaseReturns,
        FakeSalesReturnRepository SalesReturns,
        RecordingUnitOfWork Uow,
        StubCurrentUser User,
        List<string> Calls);

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
            new FakeSettlementRepository(calls),
            new FakePurchaseOrderRepository(calls),
            new FakeSalesOrderRepository(calls),
            new FakePurchaseReturnRepository(calls),
            new FakeSalesReturnRepository(calls),
            new RecordingUnitOfWork(calls),
            new StubCurrentUser(Guid.NewGuid()),
            calls);
    }

    private static CreateSettlementRequestHandler CreateHandler(Harness harness)
        => new(
            harness.Settlements,
            harness.PurchaseOrders,
            harness.SalesOrders,
            harness.PurchaseReturns,
            harness.SalesReturns,
            new PartnerRepository(harness.Context),
            harness.Uow,
            harness.User);

    private static VoidSettlementRequestHandler CreateVoidHandler(Harness harness)
        => new(
            harness.Settlements,
            harness.PurchaseOrders,
            harness.SalesOrders,
            harness.PurchaseReturns,
            harness.SalesReturns,
            harness.Uow,
            harness.User);

    private static SalesOrder NewSalesOrder(Guid partnerId, decimal totalAmount = 1000m, OrderStatus status = OrderStatus.Normal)
    {
        var now = DateTimeOffset.UtcNow;
        return new SalesOrder
        {
            Id = Guid.NewGuid(),
            OrderNo = "SO202512200001",
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

    private static PurchaseOrder NewPurchaseOrder(Guid partnerId, decimal totalAmount = 1000m, OrderStatus status = OrderStatus.Normal)
    {
        var now = DateTimeOffset.UtcNow;
        return new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            OrderNo = "PO202512200001",
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

    private static PurchaseReturn NewPurchaseReturn(Guid partnerId, decimal totalAmount = 1000m, OrderStatus status = OrderStatus.Normal)
    {
        var now = DateTimeOffset.UtcNow;
        return new PurchaseReturn
        {
            Id = Guid.NewGuid(),
            ReturnNo = "PR202512200001",
            PartnerId = partnerId,
            PartnerName = "往来一",
            ReturnDate = OrderDate,
            TotalAmount = totalAmount,
            SettledAmount = 0m,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static SalesReturn NewSalesReturn(Guid partnerId, decimal totalAmount = 1000m, OrderStatus status = OrderStatus.Normal)
    {
        var now = DateTimeOffset.UtcNow;
        return new SalesReturn
        {
            Id = Guid.NewGuid(),
            ReturnNo = "SR202512200001",
            PartnerId = partnerId,
            PartnerName = "往来一",
            ReturnDate = OrderDate,
            TotalAmount = totalAmount,
            SettledAmount = 0m,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static CreateSettlementRequest Request(
        Guid partnerId,
        SettlementType type,
        SettlementMethod method,
        params CreateSettlementItem[] items)
        => new()
        {
            Type = type,
            PartnerId = partnerId,
            SettlementDate = SettlementDate,
            Method = method,
            Items = items,
        };

    private static CreateSettlementItem Line(SettlementOrderType orderType, Guid orderId, decimal amount)
        => new() { OrderType = orderType, OrderId = orderId, Amount = amount };

    // ============================== CreateSettlement 成功 ==============================

    [Fact]
    public async Task 新增收款单_核销销售出库单_应落单并累加已结金额()
    {
        var harness = await CreateHarnessAsync();
        var order = NewSalesOrder(harness.Partner.Id);
        harness.SalesOrders.Seed(order, Array.Empty<SalesOrderItem>());

        var result = await CreateHandler(harness).HandleAsync(
            Request(harness.Partner.Id, SettlementType.Receipt, SettlementMethod.BankTransfer,
                Line(SettlementOrderType.SalesOutbound, order.Id, 400m)));

        // 单号前缀 RC + 日期；总额由后端按 Σ 核销金额重算
        Assert.Matches("^RC20260101\\d{4}$", result.SettlementNo);
        Assert.Equal((int)SettlementType.Receipt, result.Type);
        Assert.Equal(400m, result.TotalAmount);
        Assert.Equal((int)OrderStatus.Normal, result.Status);
        Assert.Equal(harness.Partner.Name, result.PartnerName);

        // 核销明细快照
        var line = Assert.Single(result.Items);
        Assert.Equal((int)SettlementOrderType.SalesOutbound, line.OrderType);
        Assert.Equal(order.Id.ToString(), line.OrderId);
        Assert.Equal(order.OrderNo, line.OrderNo);
        Assert.Equal(order.OrderDate, line.OrderDate);
        Assert.Equal(1000m, line.OrderTotalAmount);
        Assert.Equal(400m, line.Amount);

        // 单据已结算金额被原子累加，且事务提交
        var (updated, _) = await harness.SalesOrders.GetDetailAsync(order.Id);
        Assert.Equal(400m, updated!.SettledAmount);
        Assert.Contains("AddSettledAmount", harness.Calls);
        Assert.Contains("Commit", harness.Calls);
    }

    [Fact]
    public async Task 新增付款单_核销采购入库单_应落单并累加已结金额()
    {
        var harness = await CreateHarnessAsync(PartnerType.Supplier);
        var order = NewPurchaseOrder(harness.Partner.Id);
        harness.PurchaseOrders.Seed(order, Array.Empty<PurchaseOrderItem>());

        var result = await CreateHandler(harness).HandleAsync(
            Request(harness.Partner.Id, SettlementType.Payment, SettlementMethod.Cash,
                Line(SettlementOrderType.PurchaseInbound, order.Id, 250m)));

        Assert.Matches("^PY20260101\\d{4}$", result.SettlementNo);
        Assert.Equal(250m, result.TotalAmount);
        var (updated, _) = await harness.PurchaseOrders.GetDetailAsync(order.Id);
        Assert.Equal(250m, updated!.SettledAmount);
        Assert.Contains("Commit", harness.Calls);
    }

    [Fact]
    public async Task 新增收款单_核销采购退货单_应落单并累加已结金额()
    {
        var harness = await CreateHarnessAsync(PartnerType.Both);
        var purchaseReturn = NewPurchaseReturn(harness.Partner.Id);
        harness.PurchaseReturns.Seed(purchaseReturn, Array.Empty<PurchaseReturnItem>());

        var result = await CreateHandler(harness).HandleAsync(
            Request(harness.Partner.Id, SettlementType.Receipt, SettlementMethod.Other,
                Line(SettlementOrderType.PurchaseReturn, purchaseReturn.Id, 1000m)));

        Assert.Matches("^RC20260101\\d{4}$", result.SettlementNo);
        var (updated, _) = await harness.PurchaseReturns.GetDetailAsync(purchaseReturn.Id);
        Assert.Equal(1000m, updated!.SettledAmount);
    }

    [Fact]
    public async Task 新增付款单_核销销售退货单_应落单并累加已结金额()
    {
        var harness = await CreateHarnessAsync(PartnerType.Both);
        var salesReturn = NewSalesReturn(harness.Partner.Id);
        harness.SalesReturns.Seed(salesReturn, Array.Empty<SalesReturnItem>());

        var result = await CreateHandler(harness).HandleAsync(
            Request(harness.Partner.Id, SettlementType.Payment, SettlementMethod.Cash,
                Line(SettlementOrderType.SalesReturn, salesReturn.Id, 600m)));

        Assert.Matches("^PY20260101\\d{4}$", result.SettlementNo);
        var (updated, _) = await harness.SalesReturns.GetDetailAsync(salesReturn.Id);
        Assert.Equal(600m, updated!.SettledAmount);
    }

    [Fact]
    public async Task 新增收款单_多行核销_总额应为各行合计()
    {
        var harness = await CreateHarnessAsync();
        var order1 = NewSalesOrder(harness.Partner.Id, 1000m);
        var order2 = NewSalesOrder(harness.Partner.Id, 300m);
        harness.SalesOrders.Seed(order1, Array.Empty<SalesOrderItem>());
        harness.SalesOrders.Seed(order2, Array.Empty<SalesOrderItem>());

        var result = await CreateHandler(harness).HandleAsync(
            Request(harness.Partner.Id, SettlementType.Receipt, SettlementMethod.Cash,
                Line(SettlementOrderType.SalesOutbound, order1.Id, 400m),
                Line(SettlementOrderType.SalesOutbound, order2.Id, 300m)));

        Assert.Equal(700m, result.TotalAmount);
        Assert.Equal(2, result.Items.Count);
    }

    // ============================== CreateSettlement 异常 ==============================

    [Fact]
    public async Task 新增收付款单_明细为空_应报OrderItemsEmpty()
    {
        var harness = await CreateHarnessAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateHandler(harness).HandleAsync(
            Request(harness.Partner.Id, SettlementType.Receipt, SettlementMethod.Cash)));

        Assert.Equal(ErrorCode.OrderItemsEmpty, ex.Code);
    }

    [Fact]
    public async Task 新增收付款单_往来不存在_应报NotFound()
    {
        var harness = await CreateHarnessAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateHandler(harness).HandleAsync(
            Request(Guid.NewGuid(), SettlementType.Receipt, SettlementMethod.Cash,
                Line(SettlementOrderType.SalesOutbound, Guid.NewGuid(), 10m))));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 新增收付款单_往来已停用_应报PartnerDisabled()
    {
        var harness = await CreateHarnessAsync(partnerStatus: PartnerStatus.Disabled);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateHandler(harness).HandleAsync(
            Request(harness.Partner.Id, SettlementType.Receipt, SettlementMethod.Cash,
                Line(SettlementOrderType.SalesOutbound, Guid.NewGuid(), 10m))));

        Assert.Equal(ErrorCode.PartnerDisabled, ex.Code);
    }

    [Fact]
    public async Task 新增收款单_往来为纯供应商_应报PartnerTypeMismatch()
    {
        var harness = await CreateHarnessAsync(PartnerType.Supplier);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateHandler(harness).HandleAsync(
            Request(harness.Partner.Id, SettlementType.Receipt, SettlementMethod.Cash,
                Line(SettlementOrderType.SalesOutbound, Guid.NewGuid(), 10m))));

        Assert.Equal(ErrorCode.PartnerTypeMismatch, ex.Code);
    }

    [Fact]
    public async Task 新增收款单_核销采购入库单_应报DirectionMismatch()
    {
        var harness = await CreateHarnessAsync(PartnerType.Both);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateHandler(harness).HandleAsync(
            Request(harness.Partner.Id, SettlementType.Receipt, SettlementMethod.Cash,
                Line(SettlementOrderType.PurchaseInbound, Guid.NewGuid(), 10m))));

        Assert.Equal(ErrorCode.SettlementDirectionMismatch, ex.Code);
        Assert.DoesNotContain("AddSettledAmount", harness.Calls);
    }

    [Fact]
    public async Task 新增收款单_被核销单据不存在_应报NotFound且不累加()
    {
        var harness = await CreateHarnessAsync();

        await Assert.ThrowsAsync<BusinessException>(() => CreateHandler(harness).HandleAsync(
            Request(harness.Partner.Id, SettlementType.Receipt, SettlementMethod.Cash,
                Line(SettlementOrderType.SalesOutbound, Guid.NewGuid(), 10m))));

        Assert.DoesNotContain("Add", harness.Calls);
        Assert.DoesNotContain("AddSettledAmount", harness.Calls);
        Assert.DoesNotContain("Commit", harness.Calls);
    }

    [Fact]
    public async Task 新增收款单_被核销单据已作废_应报OrderVoided()
    {
        var harness = await CreateHarnessAsync();
        var order = NewSalesOrder(harness.Partner.Id, status: OrderStatus.Voided);
        harness.SalesOrders.Seed(order, Array.Empty<SalesOrderItem>());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateHandler(harness).HandleAsync(
            Request(harness.Partner.Id, SettlementType.Receipt, SettlementMethod.Cash,
                Line(SettlementOrderType.SalesOutbound, order.Id, 10m))));

        Assert.Equal(ErrorCode.OrderVoided, ex.Code);
        Assert.DoesNotContain("AddSettledAmount", harness.Calls);
    }

    [Fact]
    public async Task 新增收款单_往来与单据不一致_应报PartnerMismatch()
    {
        var harness = await CreateHarnessAsync();
        var order = NewSalesOrder(Guid.NewGuid()); // 单据属于另一个客户
        harness.SalesOrders.Seed(order, Array.Empty<SalesOrderItem>());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateHandler(harness).HandleAsync(
            Request(harness.Partner.Id, SettlementType.Receipt, SettlementMethod.Cash,
                Line(SettlementOrderType.SalesOutbound, order.Id, 10m))));

        Assert.Equal(ErrorCode.SettlementPartnerMismatch, ex.Code);
        Assert.Contains(order.OrderNo, ex.Message);
    }

    [Fact]
    public async Task 新增收款单_核销金额超过未结金额_应报AmountExceeded且不累加()
    {
        var harness = await CreateHarnessAsync();
        var order = NewSalesOrder(harness.Partner.Id, 1000m);
        order.SettledAmount = 600m; // 未结 400
        harness.SalesOrders.Seed(order, Array.Empty<SalesOrderItem>());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateHandler(harness).HandleAsync(
            Request(harness.Partner.Id, SettlementType.Receipt, SettlementMethod.Cash,
                Line(SettlementOrderType.SalesOutbound, order.Id, 400.01m))));

        Assert.Equal(ErrorCode.SettlementAmountExceeded, ex.Code);
        Assert.Contains(order.OrderNo, ex.Message);
        Assert.Contains("400.00", ex.Message);
        Assert.DoesNotContain("AddSettledAmount", harness.Calls);
    }

    [Fact]
    public async Task 新增收款单_落单失败_应回滚且不遗留已结金额()
    {
        var harness = await CreateHarnessAsync();
        var order = NewSalesOrder(harness.Partner.Id);
        harness.SalesOrders.Seed(order, Array.Empty<SalesOrderItem>());
        harness.Settlements.AddFailure = () => new InvalidOperationException("db down");

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler(harness).HandleAsync(
            Request(harness.Partner.Id, SettlementType.Receipt, SettlementMethod.Cash,
                Line(SettlementOrderType.SalesOutbound, order.Id, 400m))));

        Assert.Contains("Rollback", harness.Calls);
        Assert.DoesNotContain("Commit", harness.Calls);
    }

    // ============================== VoidSettlement ==============================

    [Fact]
    public async Task 作废收付款单_成功_应逐行回退已结金额并置作废()
    {
        var harness = await CreateHarnessAsync();
        var order = NewSalesOrder(harness.Partner.Id, 1000m);
        order.SettledAmount = 400m;
        harness.SalesOrders.Seed(order, Array.Empty<SalesOrderItem>());

        var settlement = new Settlement
        {
            Id = Guid.NewGuid(),
            SettlementNo = "RC202601010001",
            Type = SettlementType.Receipt,
            PartnerId = harness.Partner.Id,
            PartnerName = harness.Partner.Name,
            SettlementDate = SettlementDate,
            TotalAmount = 400m,
            Method = SettlementMethod.Cash,
            Status = OrderStatus.Normal,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        harness.Settlements.Seed(settlement, new[]
        {
            new SettlementItem
            {
                Id = Guid.NewGuid(),
                SettlementId = settlement.Id,
                OrderType = SettlementOrderType.SalesOutbound,
                OrderId = order.Id,
                OrderNo = order.OrderNo,
                OrderDate = order.OrderDate,
                OrderTotalAmount = 1000m,
                Amount = 400m,
            },
        });

        var result = await CreateVoidHandler(harness).HandleAsync(new VoidSettlementRequest { Id = settlement.Id });

        Assert.Equal((int)OrderStatus.Voided, result.Status);
        var (updated, _) = await harness.SalesOrders.GetDetailAsync(order.Id);
        Assert.Equal(0m, updated!.SettledAmount);
        Assert.Contains("Begin", harness.Calls);
        Assert.Contains("Commit", harness.Calls);
        Assert.DoesNotContain("Rollback", harness.Calls);
    }

    [Fact]
    public async Task 作废收付款单_不存在_应报NotFound()
    {
        var harness = await CreateHarnessAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateVoidHandler(harness).HandleAsync(new VoidSettlementRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 作废收付款单_已作废_应报OrderVoided且不重复回退()
    {
        var harness = await CreateHarnessAsync();
        var order = NewSalesOrder(harness.Partner.Id, 1000m);
        order.SettledAmount = 400m;
        harness.SalesOrders.Seed(order, Array.Empty<SalesOrderItem>());

        var settlement = new Settlement
        {
            Id = Guid.NewGuid(),
            SettlementNo = "RC202601010001",
            Type = SettlementType.Receipt,
            PartnerId = harness.Partner.Id,
            PartnerName = harness.Partner.Name,
            SettlementDate = SettlementDate,
            TotalAmount = 400m,
            Method = SettlementMethod.Cash,
            Status = OrderStatus.Voided,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        harness.Settlements.Seed(settlement, new[]
        {
            new SettlementItem
            {
                Id = Guid.NewGuid(),
                SettlementId = settlement.Id,
                OrderType = SettlementOrderType.SalesOutbound,
                OrderId = order.Id,
                OrderNo = order.OrderNo,
                OrderDate = order.OrderDate,
                OrderTotalAmount = 1000m,
                Amount = 400m,
            },
        });

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateVoidHandler(harness).HandleAsync(new VoidSettlementRequest { Id = settlement.Id }));

        Assert.Equal(ErrorCode.OrderVoided, ex.Code);
        Assert.DoesNotContain("AddSettledAmount", harness.Calls);
        var (unchanged, _) = await harness.SalesOrders.GetDetailAsync(order.Id);
        Assert.Equal(400m, unchanged!.SettledAmount);
    }
}
