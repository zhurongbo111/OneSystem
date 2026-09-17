using App.Core.Entities;
using App.Core.Features.PurchaseOrders.CreatePurchaseOrder;
using App.Core.Features.PurchaseOrders.GetPurchaseOrders;
using App.Core.Features.PurchaseOrders.UpdatePurchaseOrder;
using App.Core.Features.SalesOrders.CreateSalesOrder;
using App.Core.Features.SalesOrders.GetSalesOrders;
using App.Core.Features.SalesOrders.UpdateSalesOrder;
using App.Infrastructure;

namespace App.Tests;

/// <summary>
/// 两段式单据（erp-order-flow）字段约束一致性测试：
/// 订单 / 出入库单的单号列与关联订单号快照列长度同源（<see cref="OrderFieldConstraints"/>），
/// 明细快照列与商品档案同源（<see cref="ProductFieldConstraints"/>）；
/// 下单 / 预计到货日期边界与明细行数上限在创建 / 编辑两处一致；订单查询关键词不超过订单号列长。
/// </summary>
public class OrderFlowFieldConsistencyTests
{
    private static readonly DateTimeOffset OrderDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static int GetMaxLength<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetMaxLength()!.Value;

    // ============================== EF 模型 ←→ 常量 ==============================

    [Fact]
    public void EF模型_订单主表列长度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(OrderFieldConstraints.OrderNoMaxLength, GetMaxLength<PurchaseOrder>(dbContext, nameof(PurchaseOrder.OrderNo)));
        Assert.Equal(OrderFieldConstraints.RemarkMaxLength, GetMaxLength<PurchaseOrder>(dbContext, nameof(PurchaseOrder.Remark)));
        Assert.Equal(OrderFieldConstraints.OrderNoMaxLength, GetMaxLength<SalesOrder>(dbContext, nameof(SalesOrder.OrderNo)));
        Assert.Equal(OrderFieldConstraints.RemarkMaxLength, GetMaxLength<SalesOrder>(dbContext, nameof(SalesOrder.Remark)));
    }

    [Fact]
    public void EF模型_出入库单号与关联订单号快照列长度_应同源()
    {
        using var dbContext = TestSupport.CreateDbContext();

        // 自身单号（ReceiptNo / ShipmentNo）与关联订单号快照（OrderNo）共用 OrderFieldConstraints.OrderNoMaxLength
        Assert.Equal(OrderFieldConstraints.OrderNoMaxLength, GetMaxLength<PurchaseReceipt>(dbContext, nameof(PurchaseReceipt.ReceiptNo)));
        Assert.Equal(OrderFieldConstraints.OrderNoMaxLength, GetMaxLength<PurchaseReceipt>(dbContext, nameof(PurchaseReceipt.OrderNo)));
        Assert.Equal(OrderFieldConstraints.OrderNoMaxLength, GetMaxLength<SalesShipment>(dbContext, nameof(SalesShipment.ShipmentNo)));
        Assert.Equal(OrderFieldConstraints.OrderNoMaxLength, GetMaxLength<SalesShipment>(dbContext, nameof(SalesShipment.OrderNo)));
    }

    [Fact]
    public void EF模型_订单明细快照列长度_应等于商品字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(ProductFieldConstraints.NameMaxLength, GetMaxLength<PurchaseOrderItem>(dbContext, nameof(PurchaseOrderItem.ProductName)));
        Assert.Equal(ProductFieldConstraints.UnitMaxLength, GetMaxLength<PurchaseOrderItem>(dbContext, nameof(PurchaseOrderItem.Unit)));
        Assert.Equal(ProductFieldConstraints.NameMaxLength, GetMaxLength<SalesOrderItem>(dbContext, nameof(SalesOrderItem.ProductName)));
        Assert.Equal(ProductFieldConstraints.UnitMaxLength, GetMaxLength<SalesOrderItem>(dbContext, nameof(SalesOrderItem.Unit)));
    }

    // ============================== 预计日期边界（创建 / 编辑一致）==============================

    private static CreatePurchaseOrderRequest NewCreatePurchase(DateTimeOffset? expectedDate, int itemCount = 1)
        => new()
        {
            PartnerId = Guid.NewGuid(),
            OrderDate = OrderDate,
            ExpectedDate = expectedDate,
            Items = Enumerable.Range(0, itemCount)
                .Select(_ => new CreatePurchaseOrderItem { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 1m })
                .ToList(),
        };

    private static UpdatePurchaseOrderRequest NewUpdatePurchase(DateTimeOffset? expectedDate, int itemCount = 1)
        => new()
        {
            Id = Guid.NewGuid(),
            PartnerId = Guid.NewGuid(),
            OrderDate = OrderDate,
            ExpectedDate = expectedDate,
            Items = Enumerable.Range(0, itemCount)
                .Select(_ => new UpdatePurchaseOrderItem { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 1m })
                .ToList(),
        };

    private static CreateSalesOrderRequest NewCreateSales(DateTimeOffset? expectedDate, int itemCount = 1)
        => new()
        {
            PartnerId = Guid.NewGuid(),
            OrderDate = OrderDate,
            ExpectedDate = expectedDate,
            Items = Enumerable.Range(0, itemCount)
                .Select(_ => new CreateSalesOrderItem { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 1m })
                .ToList(),
        };

    private static UpdateSalesOrderRequest NewUpdateSales(DateTimeOffset? expectedDate, int itemCount = 1)
        => new()
        {
            Id = Guid.NewGuid(),
            PartnerId = Guid.NewGuid(),
            OrderDate = OrderDate,
            ExpectedDate = expectedDate,
            Items = Enumerable.Range(0, itemCount)
                .Select(_ => new UpdateSalesOrderItem { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 1m })
                .ToList(),
        };

    [Fact]
    public void 采购订单_预计到货日期边界_早于下单拒绝_等于与空通过()
    {
        var createValidator = new CreatePurchaseOrderRequestValidator();
        var updateValidator = new UpdatePurchaseOrderRequestValidator();

        Assert.False(createValidator.Validate(NewCreatePurchase(OrderDate.AddDays(-1))).IsValid);
        Assert.True(createValidator.Validate(NewCreatePurchase(OrderDate)).IsValid);
        Assert.True(createValidator.Validate(NewCreatePurchase(null)).IsValid);

        Assert.False(updateValidator.Validate(NewUpdatePurchase(OrderDate.AddDays(-1))).IsValid);
        Assert.True(updateValidator.Validate(NewUpdatePurchase(OrderDate)).IsValid);
    }

    [Fact]
    public void 销售订单_预计发货日期边界_早于下单拒绝_等于与空通过()
    {
        var createValidator = new CreateSalesOrderRequestValidator();
        var updateValidator = new UpdateSalesOrderRequestValidator();

        Assert.False(createValidator.Validate(NewCreateSales(OrderDate.AddDays(-1))).IsValid);
        Assert.True(createValidator.Validate(NewCreateSales(OrderDate)).IsValid);
        Assert.True(createValidator.Validate(NewCreateSales(null)).IsValid);

        Assert.False(updateValidator.Validate(NewUpdateSales(OrderDate.AddDays(-1))).IsValid);
        Assert.True(updateValidator.Validate(NewUpdateSales(OrderDate)).IsValid);
    }

    // ============================== 明细行数上限（创建 / 编辑一致）==============================

    [Fact]
    public void 采购订单明细行数上限_创建与编辑两处应一致()
    {
        var createValidator = new CreatePurchaseOrderRequestValidator();
        var updateValidator = new UpdatePurchaseOrderRequestValidator();

        Assert.True(createValidator.Validate(NewCreatePurchase(null, OrderFieldConstraints.ItemsMaxCount)).IsValid);
        Assert.False(createValidator.Validate(NewCreatePurchase(null, OrderFieldConstraints.ItemsMaxCount + 1)).IsValid);
        Assert.True(updateValidator.Validate(NewUpdatePurchase(null, OrderFieldConstraints.ItemsMaxCount)).IsValid);
        Assert.False(updateValidator.Validate(NewUpdatePurchase(null, OrderFieldConstraints.ItemsMaxCount + 1)).IsValid);
    }

    [Fact]
    public void 销售订单明细行数上限_创建与编辑两处应一致()
    {
        var createValidator = new CreateSalesOrderRequestValidator();
        var updateValidator = new UpdateSalesOrderRequestValidator();

        Assert.True(createValidator.Validate(NewCreateSales(null, OrderFieldConstraints.ItemsMaxCount)).IsValid);
        Assert.False(createValidator.Validate(NewCreateSales(null, OrderFieldConstraints.ItemsMaxCount + 1)).IsValid);
        Assert.True(updateValidator.Validate(NewUpdateSales(null, OrderFieldConstraints.ItemsMaxCount)).IsValid);
        Assert.False(updateValidator.Validate(NewUpdateSales(null, OrderFieldConstraints.ItemsMaxCount + 1)).IsValid);
    }

    // ============================== 查询关键词 ←→ 订单号列长 ==============================

    [Fact]
    public void 采购订单查询关键词长度_应不超过订单号列长()
    {
        var ok = new string('a', OrderFieldConstraints.KeywordMaxLength);
        var tooLong = new string('a', OrderFieldConstraints.KeywordMaxLength + 1);
        var validator = new GetPurchaseOrdersRequestValidator();

        Assert.True(validator.Validate(new GetPurchaseOrdersRequest { Page = 1, PageSize = 20, Keyword = ok }).IsValid);
        Assert.False(validator.Validate(new GetPurchaseOrdersRequest { Page = 1, PageSize = 20, Keyword = tooLong }).IsValid);
    }

    [Fact]
    public void 销售订单查询关键词长度_应不超过订单号列长()
    {
        var ok = new string('a', OrderFieldConstraints.KeywordMaxLength);
        var tooLong = new string('a', OrderFieldConstraints.KeywordMaxLength + 1);
        var validator = new GetSalesOrdersRequestValidator();

        Assert.True(validator.Validate(new GetSalesOrdersRequest { Page = 1, PageSize = 20, Keyword = ok }).IsValid);
        Assert.False(validator.Validate(new GetSalesOrdersRequest { Page = 1, PageSize = 20, Keyword = tooLong }).IsValid);
    }

    // ============================== 流转状态取值 ==============================

    [Fact]
    public void 订单流转状态筛选_非法取值应被拒绝()
    {
        var purchaseValidator = new GetPurchaseOrdersRequestValidator();
        var salesValidator = new GetSalesOrdersRequestValidator();

        // 合法：全部枚举值
        foreach (var status in Enum.GetValues<OrderFlowStatus>())
        {
            Assert.True(purchaseValidator.Validate(new GetPurchaseOrdersRequest { Page = 1, PageSize = 20, FlowStatus = status }).IsValid);
            Assert.True(salesValidator.Validate(new GetSalesOrdersRequest { Page = 1, PageSize = 20, FlowStatus = status }).IsValid);
        }

        // 非法：枚举外取值
        var invalid = (OrderFlowStatus)99;
        Assert.False(purchaseValidator.Validate(new GetPurchaseOrdersRequest { Page = 1, PageSize = 20, FlowStatus = invalid }).IsValid);
        Assert.False(salesValidator.Validate(new GetSalesOrdersRequest { Page = 1, PageSize = 20, FlowStatus = invalid }).IsValid);
    }
}
