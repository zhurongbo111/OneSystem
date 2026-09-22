using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Products.UpdateProductStatus;
using App.Infrastructure;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// UpdateProductStatusRequestHandler 测试：停用 / 启用切换、商品不存在
/// </summary>
public class UpdateProductStatusRequestHandlerTests
{
    private static (AppDbContext Context, UpdateProductStatusRequestHandler Handler) CreateHandler()
    {
        var context = TestSupport.CreateDbContext();
        var handler = new UpdateProductStatusRequestHandler(
            new ProductRepository(context),
            new CategoryRepository(context),
            new InventoryRepository(context), TestSupport.AuditLogger);
        return (context, handler);
    }

    private static async Task<Product> SeedAsync(AppDbContext context)
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "原材料", CreatedAt = DateTimeOffset.UtcNow };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Code = "sku-status",
            Name = "状态商品",
            CategoryId = category.Id,
            Unit = "个",
            Status = ProductStatus.Enabled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        context.Categories.Add(category);
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    [Fact]
    public async Task 停用商品_状态应改为Disabled()
    {
        var (context, handler) = CreateHandler();
        var product = await SeedAsync(context);

        var result = await handler.HandleAsync(new UpdateProductStatusRequest
        {
            Id = product.Id,
            Status = (int)ProductStatus.Disabled,
        });

        Assert.Equal((int)ProductStatus.Disabled, result.Status);
        Assert.Equal(ProductStatus.Disabled, (await context.Products.SingleAsync(p => p.Id == product.Id)).Status);
    }

    [Fact]
    public async Task 启用已停用商品_状态应改回Enabled()
    {
        var (context, handler) = CreateHandler();
        var product = await SeedAsync(context);
        product.Status = ProductStatus.Disabled;
        await context.SaveChangesAsync();

        var result = await handler.HandleAsync(new UpdateProductStatusRequest
        {
            Id = product.Id,
            Status = (int)ProductStatus.Enabled,
        });

        Assert.Equal((int)ProductStatus.Enabled, result.Status);
    }

    [Fact]
    public async Task 停用启用_商品不存在_应报NotFound()
    {
        var (_, handler) = CreateHandler();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdateProductStatusRequest
        {
            Id = Guid.NewGuid(),
            Status = (int)ProductStatus.Disabled,
        }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }
}
