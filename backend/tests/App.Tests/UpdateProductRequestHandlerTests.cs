using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Products.UpdateProduct;
using App.Infrastructure;
using App.Infrastructure.Persistence;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// UpdateProductRequestHandler 测试：商品 / 分类存在校验、编码不可改
/// </summary>
public class UpdateProductRequestHandlerTests
{
    private static (AppDbContext Context, UpdateProductRequestHandler Handler) CreateHandler()
    {
        var context = TestSupport.CreateDbContext();
        var handler = new UpdateProductRequestHandler(
            new ProductRepository(context),
            new CategoryRepository(context),
            new InventoryRepository(context),
            new UnitOfWork(context),
            new StubCurrentUser(Guid.NewGuid()), TestSupport.AuditLogger);
        return (context, handler);
    }

    private static async Task<(Product Product, Category Category, Inventory Inventory)> SeedAsync(AppDbContext context)
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "原材料", CreatedAt = DateTimeOffset.UtcNow };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Code = "sku-upd",
            Name = "旧名称",
            CategoryId = category.Id,
            Unit = "个",
            PurchasePrice = 1m,
            SalePrice = 2m,
            SafetyStock = 0,
            Status = ProductStatus.Enabled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        var inventory = new Inventory { Id = Guid.NewGuid(), ProductId = product.Id, Quantity = 7, UpdatedAt = DateTimeOffset.UtcNow };
        context.Categories.Add(category);
        context.Products.Add(product);
        context.Inventory.Add(inventory);
        await context.SaveChangesAsync();
        return (product, category, inventory);
    }

    [Fact]
    public async Task 编辑商品_应成功且不改变编码与库存()
    {
        var (context, handler) = CreateHandler();
        var (product, category, _) = await SeedAsync(context);

        var result = await handler.HandleAsync(new UpdateProductRequest
        {
            Id = product.Id,
            Name = "新名称",
            CategoryId = category.Id,
            Unit = "箱",
            PurchasePrice = 5m,
            SalePrice = 9m,
            SafetyStock = 10,
        });

        Assert.Equal("sku-upd", result.Code);
        Assert.Equal("新名称", result.Name);
        Assert.Equal("箱", result.Unit);
        Assert.Equal(5m, result.PurchasePrice);
        Assert.Equal(7, result.StockQuantity);
    }

    [Fact]
    public async Task 编辑商品_商品不存在_应报NotFound()
    {
        var (context, handler) = CreateHandler();
        var (_, category, _) = await SeedAsync(context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdateProductRequest
        {
            Id = Guid.NewGuid(),
            Name = "新名称",
            CategoryId = category.Id,
            Unit = "个",
        }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 编辑商品_分类不存在_应报NotFound()
    {
        var (context, handler) = CreateHandler();
        var (product, _, _) = await SeedAsync(context);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdateProductRequest
        {
            Id = product.Id,
            Name = "新名称",
            CategoryId = Guid.NewGuid(),
            Unit = "个",
        }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }
}
