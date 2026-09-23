using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.SalesShipments.CreateSalesShipment;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 销售出库信用额度校验测试（specs/036-erp-partner-price/design.md §0.4）：
/// 额度 0 视为不限且不查询应收；「应收 + 本单 ≤ 额度」放行；超限报 40130 且 message 含额度 / 应收 / 本单三个金额；
/// 校验位于事务与库存扣减之前 → 失败路径不产生库存扣减 / 流水 / 单据。
/// 应收取数复用 <see cref="FakeSettlementQueryRepository.GetReceivableAmountAsync"/>（`023` 同一仓储方法，与往来对账页同源）。
/// </summary>
public class PartnerCreditLimitTests
{
    private static readonly DateTimeOffset OrderDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (AppDbContext Context, FakeSalesShipmentRepository Orders, FakeInventoryRepository Inventory,
        FakeStockMovementRepository Movements, FakeSettlementQueryRepository Settlement, RecordingUnitOfWork Uow,
        CreateSalesShipmentRequestHandler Handler, List<string> Calls) CreateHandler()
    {
        var context = TestSupport.CreateDbContext();
        var calls = new List<string>();
        var orders = new FakeSalesShipmentRepository(calls);
        var inventory = new FakeInventoryRepository(calls);
        var movements = new FakeStockMovementRepository(calls);
        var settlement = new FakeSettlementQueryRepository();
        var uow = new RecordingUnitOfWork(calls);
        var gl = GeneralLedgerStubs.Create();
        var handler = new CreateSalesShipmentRequestHandler(
            orders,
            new FakeSalesOrderRepository(calls),
            new PartnerRepository(context),
            new ProductRepository(context),
            inventory,
            movements,
            settlement,
            gl.Vouchers, gl.Mappings, gl.Periods, gl.Accounts,
            uow,
            new StubCurrentUser(Guid.NewGuid()),
            TestSupport.AuditLogger);
        return (context, orders, inventory, movements, settlement, uow, handler, calls);
    }

    private static async Task<(Partner Partner, Product Product)> SeedAsync(
        AppDbContext context, FakeInventoryRepository inventory, decimal creditLimit)
    {
        var partner = TestSupport.NewPartner("客户甲", type: PartnerType.Customer);
        partner.CreditLimit = creditLimit;
        context.Partners.Add(partner);
        var product = TestSupport.NewProduct("sku-cl-1", "协议商品", 1m);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        inventory.Seed(product.Id, 100);
        return (partner, product);
    }

    private static CreateSalesShipmentRequest RequestWith(Guid partnerId, Guid productId, int quantity, decimal unitPrice)
        => new()
        {
            PartnerId = partnerId,
            OrderDate = OrderDate,
            Items = [new CreateSalesShipmentItem { ProductId = productId, Quantity = quantity, UnitPrice = unitPrice }],
        };

    [Fact]
    public async Task 信用校验_额度为0_应跳过校验且不查询应收()
    {
        var (context, _, inventory, _, settlement, _, handler, _) = CreateHandler();
        var (partner, product) = await SeedAsync(context, inventory, creditLimit: 0m);
        settlement.ReceivableAmount = 500m;

        var result = await handler.HandleAsync(RequestWith(partner.Id, product.Id, 3, 10m));

        Assert.Equal(30m, result.TotalAmount);
        Assert.Empty(settlement.ReceivableQueries);
    }

    [Fact]
    public async Task 信用校验_应收加本单等于额度_应放行且查询该客户应收()
    {
        var (context, _, inventory, _, settlement, _, handler, _) = CreateHandler();
        var (partner, product) = await SeedAsync(context, inventory, creditLimit: 1000m);
        settlement.ReceivableAmount = 700m;

        var result = await handler.HandleAsync(RequestWith(partner.Id, product.Id, 3, 100m)); // 700 + 300 = 1000

        Assert.Equal(300m, result.TotalAmount);
        Assert.Equal([partner.Id], settlement.ReceivableQueries);
    }

    [Fact]
    public async Task 信用校验_应收加本单超出额度_应报CreditLimitExceeded()
    {
        var (context, _, inventory, _, settlement, _, handler, _) = CreateHandler();
        var (partner, product) = await SeedAsync(context, inventory, creditLimit: 1000m);
        settlement.ReceivableAmount = 800m;

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            handler.HandleAsync(RequestWith(partner.Id, product.Id, 3, 100m))); // 800 + 300 > 1000

        Assert.Equal(ErrorCode.CreditLimitExceeded, ex.Code);
        Assert.Contains("客户甲", ex.Message);
        Assert.Contains("1000.00", ex.Message);
        Assert.Contains("800.00", ex.Message);
        Assert.Contains("300.00", ex.Message);
    }

    [Fact]
    public async Task 信用校验_超限_不应产生库存扣减流水与单据()
    {
        var (context, orders, inventory, movements, settlement, _, handler, calls) = CreateHandler();
        var (partner, product) = await SeedAsync(context, inventory, creditLimit: 1000m);
        settlement.ReceivableAmount = 800m;

        await Assert.ThrowsAsync<BusinessException>(() =>
            handler.HandleAsync(RequestWith(partner.Id, product.Id, 3, 100m)));

        Assert.Empty(inventory.Decrements);          // 未扣库存：校验在扣减之前
        Assert.Empty(movements.Appended);            // 未写流水
        Assert.Empty(orders.Orders);                 // 未插单据
        Assert.DoesNotContain("Begin", calls);       // 未进事务
        Assert.DoesNotContain("Generate", calls);
        Assert.DoesNotContain("Add", calls);
        Assert.DoesNotContain("Commit", calls);
        Assert.Equal(100, inventory.GetQuantity(product.Id));
        Assert.DoesNotContain("Rollback", calls);   // 校验在事务之前，无需回滚
    }
}
