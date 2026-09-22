using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.PurchaseReceipts.CreatePurchaseReceipt;
using App.Core.Features.PurchaseReceipts.VoidPurchaseReceipt;
using App.Core.Features.SalesShipments.CreateSalesShipment;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 既有单据写入路径改造测试（specs/033-erp-general-ledger tasks 6.4）：
/// 创建成功后同事务生成自动凭证且借贷平衡；作废后凭证置 <c>Voided</c>；
/// 映射缺失（40158）阻断单据创建且不提交事务；销售出库凭证含成本结转分录
/// </summary>
public class AutoVoucherWritePathTests
{
    private static readonly DateTimeOffset OrderDate = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed record Arrange(
        AppDbContext Context,
        Partner Partner,
        Product Product,
        FakePurchaseReceiptRepository Receipts,
        FakeSalesShipmentRepository Shipments,
        FakeInventoryRepository Inventory,
        FakeStockMovementRepository Movements,
        GeneralLedgerStubs Gl,
        StubCurrentUser User,
        List<string> Calls);

    private static async Task<Arrange> ArrangeAsync()
    {
        var context = TestSupport.CreateDbContext();
        var partner = TestSupport.NewPartner("自动凭证供应商", PartnerType.Both);
        var product = TestSupport.NewProduct("sku-gl-1", "总账商品");
        context.Partners.Add(partner);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        return new Arrange(
            context,
            partner,
            product,
            new FakePurchaseReceiptRepository(),
            new FakeSalesShipmentRepository(),
            new FakeInventoryRepository(),
            new FakeStockMovementRepository(),
            GeneralLedgerStubs.Create(),
            new StubCurrentUser(Guid.NewGuid()),
            []);
    }

    private static CreatePurchaseReceiptRequestHandler CreatePurchaseHandler(Arrange a)
        => new(
            a.Receipts,
            new FakePurchaseOrderRepository(),
            new PartnerRepository(a.Context),
            new ProductRepository(a.Context),
            a.Inventory,
            a.Movements,
            a.Gl.Vouchers,
            a.Gl.Mappings,
            a.Gl.Periods,
            a.Gl.Accounts,
            new RecordingUnitOfWork(a.Calls),
            a.User,
            TestSupport.AuditLogger);

    private static VoidPurchaseReceiptRequestHandler CreateVoidHandler(Arrange a)
        => new(
            a.Receipts,
            new FakePurchaseOrderRepository(),
            a.Inventory,
            a.Movements,
            a.Gl.Vouchers,
            a.Gl.Periods,
            new RecordingUnitOfWork(a.Calls),
            a.User,
            TestSupport.AuditLogger);

    private static CreatePurchaseReceiptRequest PurchaseRequest(Arrange a)
        => new()
        {
            PartnerId = a.Partner.Id,
            OrderDate = OrderDate,
            Items = [new CreatePurchaseReceiptItem { ProductId = a.Product.Id, Quantity = 2, UnitPrice = 500m }],
        };

    [Fact]
    public async Task 采购入库_应同事务生成平衡的自动凭证()
    {
        var a = await ArrangeAsync();

        var result = await CreatePurchaseHandler(a).HandleAsync(PurchaseRequest(a));

        var (voucher, entries) = Assert.Single(a.Gl.Vouchers.Appended);
        Assert.Equal(VoucherSourceType.PurchaseInbound, voucher.SourceType);
        Assert.Equal(Guid.Parse(result.Id), voucher.SourceId);
        Assert.Equal(result.ReceiptNo, voucher.SourceNo);
        Assert.Equal($"采购入库单 {result.ReceiptNo}", voucher.Summary);
        Assert.Equal(1000m, voucher.TotalDebit);
        Assert.Equal(1000m, voucher.TotalCredit);
        Assert.Equal(1000m, entries.Sum(e => e.Debit));
        Assert.Equal(entries.Sum(e => e.Debit), entries.Sum(e => e.Credit));

        // 借存货 / 贷应付账款
        Assert.Equal("1405", entries[0].AccountCode);
        Assert.Equal("2202", entries[1].AccountCode);

        await a.Context.DisposeAsync();
    }

    [Fact]
    public async Task 采购入库作废_应同事务作废自动凭证()
    {
        var a = await ArrangeAsync();
        var created = await CreatePurchaseHandler(a).HandleAsync(PurchaseRequest(a));

        await CreateVoidHandler(a).HandleAsync(new VoidPurchaseReceiptRequest { Id = Guid.Parse(created.Id) });

        var voucher = Assert.Single(a.Gl.Vouchers.All);
        Assert.Equal(VoucherStatus.Voided, voucher.Status);
        Assert.Contains((VoucherSourceType.PurchaseInbound, Guid.Parse(created.Id)), a.Gl.Vouchers.VoidedBySource);

        await a.Context.DisposeAsync();
    }

    [Fact]
    public async Task 科目映射缺失_应阻断单据创建且不提交事务()
    {
        var a = await ArrangeAsync();
        a.Gl.Mappings.Items.Clear();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreatePurchaseHandler(a).HandleAsync(PurchaseRequest(a)));

        Assert.Equal(ErrorCode.AccountMappingMissing, ex.Code);
        Assert.Empty(a.Gl.Vouchers.Appended);
        Assert.Contains("Begin", a.Calls);
        // 事务未提交 → 真实数据库下单据写入随回滚撤销（假实现不模拟回滚，故断言提交边界）
        Assert.DoesNotContain("Commit", a.Calls);

        await a.Context.DisposeAsync();
    }

    [Fact]
    public async Task 期间已结账_应阻断单据创建()
    {
        var a = await ArrangeAsync();
        a.Gl.Periods.Seed(2026, 9, PeriodStatus.Closed);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreatePurchaseHandler(a).HandleAsync(PurchaseRequest(a)));

        Assert.Equal(ErrorCode.PeriodClosed, ex.Code);
        Assert.Empty(a.Gl.Vouchers.Appended);
        Assert.DoesNotContain("Commit", a.Calls);

        await a.Context.DisposeAsync();
    }

    [Fact]
    public async Task 销售出库_自动凭证应含成本结转分录()
    {
        var a = await ArrangeAsync();
        a.Inventory.Seed(a.Product.Id, 10);
        a.Inventory.AverageCosts[a.Product.Id] = 15m;

        var handler = new CreateSalesShipmentRequestHandler(
            a.Shipments,
            new FakeSalesOrderRepository(),
            new PartnerRepository(a.Context),
            new ProductRepository(a.Context),
            a.Inventory,
            a.Movements,
            a.Gl.Vouchers,
            a.Gl.Mappings,
            a.Gl.Periods,
            a.Gl.Accounts,
            new RecordingUnitOfWork(a.Calls),
            a.User,
            TestSupport.AuditLogger);

        await handler.HandleAsync(new CreateSalesShipmentRequest
        {
            PartnerId = a.Partner.Id,
            OrderDate = OrderDate,
            Items = [new CreateSalesShipmentItem { ProductId = a.Product.Id, Quantity = 2, UnitPrice = 800m }],
        });

        var (voucher, entries) = Assert.Single(a.Gl.Vouchers.Appended);
        Assert.Equal(VoucherSourceType.SalesOutbound, voucher.SourceType);

        // 借应收账款 1600 / 贷主营业务收入 1600 / 借主营业务成本 30 / 贷库存商品 30
        Assert.Equal(4, entries.Count);
        Assert.Equal("1122", entries[0].AccountCode);
        Assert.Equal(1600m, entries[0].Debit);
        Assert.Equal("6001", entries[1].AccountCode);
        Assert.Equal(1600m, entries[1].Credit);
        Assert.Equal("6401", entries[2].AccountCode);
        Assert.Equal(30m, entries[2].Debit);
        Assert.Equal("1405", entries[3].AccountCode);
        Assert.Equal(30m, entries[3].Credit);
        Assert.Equal(1630m, voucher.TotalDebit);

        await a.Context.DisposeAsync();
    }
}
