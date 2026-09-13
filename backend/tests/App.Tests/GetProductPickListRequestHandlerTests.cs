using App.Core.Entities;
using App.Core.Features.Products.GetProductPickList;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// GetProductPickListRequestHandler 测试：仅启用商品、开单选择带出库存
/// </summary>
public class GetProductPickListRequestHandlerTests
{
    private static (AppDbContext Context, GetProductPickListRequestHandler Handler) CreateHandler()
    {
        var context = TestSupport.CreateDbContext();
        var handler = new GetProductPickListRequestHandler(new ProductRepository(context));
        return (context, handler);
    }

    [Fact]
    public async Task 开单选择_仅返回启用商品_停用商品被过滤()
    {
        var (context, handler) = CreateHandler();
        var category = new Category { Id = Guid.NewGuid(), Name = "原材料", CreatedAt = DateTimeOffset.UtcNow };
        context.Categories.Add(category);

        var enabled = new Product { Id = Guid.NewGuid(), Code = "pick-on", Name = "启用商品", CategoryId = category.Id, Unit = "个", Status = ProductStatus.Enabled, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var disabled = new Product { Id = Guid.NewGuid(), Code = "pick-off", Name = "停用商品", CategoryId = category.Id, Unit = "个", Status = ProductStatus.Disabled, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        context.Products.Add(enabled);
        context.Products.Add(disabled);
        context.Inventory.Add(new Inventory { Id = Guid.NewGuid(), ProductId = enabled.Id, Quantity = 3, UpdatedAt = DateTimeOffset.UtcNow });
        context.Inventory.Add(new Inventory { Id = Guid.NewGuid(), ProductId = disabled.Id, Quantity = 9, UpdatedAt = DateTimeOffset.UtcNow });
        await context.SaveChangesAsync();

        var result = await handler.HandleAsync(new GetProductPickListRequest());

        Assert.Single(result);
        Assert.Equal("pick-on", result[0].Code);
        Assert.Equal(3, result[0].StockQuantity);
    }

    [Fact]
    public async Task 开单选择_编码正序_带出库存与价格()
    {
        var (context, handler) = CreateHandler();
        var category = new Category { Id = Guid.NewGuid(), Name = "原材料", CreatedAt = DateTimeOffset.UtcNow };
        context.Categories.Add(category);
        var b = new Product { Id = Guid.NewGuid(), Code = "zeta", Name = "乙", CategoryId = category.Id, Unit = "个", PurchasePrice = 2m, SalePrice = 4m, Status = ProductStatus.Enabled, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var a = new Product { Id = Guid.NewGuid(), Code = "alpha", Name = "甲", CategoryId = category.Id, Unit = "个", PurchasePrice = 1m, SalePrice = 3m, Status = ProductStatus.Enabled, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        context.Products.Add(b);
        context.Products.Add(a);
        context.Inventory.Add(new Inventory { Id = Guid.NewGuid(), ProductId = b.Id, Quantity = 5, UpdatedAt = DateTimeOffset.UtcNow });
        context.Inventory.Add(new Inventory { Id = Guid.NewGuid(), ProductId = a.Id, Quantity = 8, UpdatedAt = DateTimeOffset.UtcNow });
        await context.SaveChangesAsync();

        var result = await handler.HandleAsync(new GetProductPickListRequest());

        Assert.Equal(2, result.Count);
        Assert.Equal("alpha", result[0].Code); // 编码正序
        Assert.Equal(1m, result[0].PurchasePrice);
        Assert.Equal(8, result[0].StockQuantity);
    }
}
