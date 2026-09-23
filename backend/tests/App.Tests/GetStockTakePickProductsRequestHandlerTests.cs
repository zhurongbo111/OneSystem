using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Features.StockTakes.GetStockTakePickProducts;

namespace App.Tests;

/// <summary>
/// GetStockTakePickProductsRequestHandler 测试（design.md §6）：hasMovements 标记正确（有流水商品为 true）、
/// 仅返回启用商品（仓储已过滤，Handler 不重复过滤）。
/// </summary>
public class GetStockTakePickProductsRequestHandlerTests
{
    private static (FakeProductRepository Products, FakeStockMovementRepository Movements, FakeInventoryRepository Inventory,
        GetStockTakePickProductsRequestHandler Handler) CreateHandler()
    {
        var products = new FakeProductRepository();
        var movements = new FakeStockMovementRepository();
        var inventory = new FakeInventoryRepository();
        var handler = new GetStockTakePickProductsRequestHandler(
            products, new FakeWarehouseRepository(), inventory, movements);
        return (products, movements, inventory, handler);
    }

    [Fact]
    public async Task 盘点商品选择_已发生变动商品_hasMovements应为true()
    {
        var (products, movements, _, handler) = CreateHandler();
        var withMovement = Guid.NewGuid();
        var clean = Guid.NewGuid();
        products.Picks.Add(new ProductPickItem
        {
            Id = withMovement,
            Code = "pick-mv",
            Name = "已建账商品",
            Unit = "个",
            PurchasePrice = 1m,
            SalePrice = 2m,
            StockQuantity = 4,
        });
        products.Picks.Add(new ProductPickItem
        {
            Id = clean,
            Code = "pick-cl",
            Name = "未建账商品",
            Unit = "个",
            PurchasePrice = 1m,
            SalePrice = 2m,
            StockQuantity = 0,
        });
        movements.Appended.Add(new StockMovement
        {
            Id = Guid.NewGuid(),
            ProductId = withMovement,
            WarehouseId = TestWarehouse.DefaultId,
            MovementType = StockMovementType.InitialStock,
            Quantity = 4,
            SourceNo = "ST202601010001",
            CreatedAt = DateTimeOffset.UtcNow,
        });

        var result = await handler.HandleAsync(new GetStockTakePickProductsRequest());

        Assert.Equal(2, result.Count);
        var mv = Assert.Single(result, p => p.Id == withMovement.ToString());
        var cl = Assert.Single(result, p => p.Id == clean.ToString());
        Assert.True(mv.HasMovements);
        Assert.False(cl.HasMovements);
    }

    [Fact]
    public async Task 盘点商品选择_仅返回仓储提供的启用商品_Handler不重复过滤()
    {
        var (products, _, inventory, handler) = CreateHandler();
        // 仓储已过滤停用商品，Handler 仅透传（Picks 中即视为启用商品）
        var only = Guid.NewGuid();
        products.Picks.Add(new ProductPickItem
        {
            Id = only,
            Code = "only",
            Name = "唯一商品",
            Unit = "箱",
            PurchasePrice = 3m,
            SalePrice = 5m,
            StockQuantity = 12,
        });
        // 038：账面数量按「所选仓」读取（不取商品下拉自带的组织级库存）
        inventory.Seed(only, 12);

        var result = await handler.HandleAsync(new GetStockTakePickProductsRequest());

        var item = Assert.Single(result);
        Assert.Equal("only", item.Code);
        Assert.Equal(12, item.StockQuantity);
        Assert.False(item.HasMovements);
    }
}
