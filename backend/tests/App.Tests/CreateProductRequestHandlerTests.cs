using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Products.CreateProduct;
using App.Core.Features.Products.GetProductById;
using App.Infrastructure;
using App.Infrastructure.Persistence;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// CreateProductRequestHandler 测试：编码唯一（大小写不敏感）、分类存在、库存行同事务初始化
/// </summary>
public class CreateProductRequestHandlerTests
{
    private static (AppDbContext Context, CreateProductRequestHandler Handler, StubCurrentUser User) CreateHandler()
    {
        var context = TestSupport.CreateDbContext();
        var user = new StubCurrentUser(Guid.NewGuid());
        var handler = new CreateProductRequestHandler(
            new ProductRepository(context),
            new CategoryRepository(context),
            new InventoryRepository(context),
            new UnitOfWork(context),
            user, TestSupport.AuditLogger);
        return (context, handler, user);
    }

    private static async Task<Category> SeedCategoryAsync(AppDbContext context, string name = "原材料")
    {
        var category = new Category { Id = Guid.NewGuid(), Name = name, CreatedAt = DateTimeOffset.UtcNow };
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category;
    }

    [Fact]
    public async Task 新增商品_应成功并初始化库存行为零()
    {
        var (context, handler, _) = CreateHandler();
        var category = await SeedCategoryAsync(context);

        var result = await handler.HandleAsync(new CreateProductRequest
        {
            Code = "sku-001",
            Name = "螺栓",
            CategoryId = category.Id,
            Unit = "个",
            PurchasePrice = 1.5m,
            SalePrice = 3.2m,
            SafetyStock = 50,
        });

        Assert.Equal("sku-001", result.Code);
        Assert.Equal((int)ProductStatus.Enabled, result.Status);
        Assert.Equal(0, result.StockQuantity);
        Assert.True(result.IsBelowSafetyStock); // 0 < 50
        Assert.Equal(category.Name, result.CategoryName);

        var product = await context.Products.SingleAsync(p => p.Id.ToString() == result.Id);
        var inventory = await context.Inventory.SingleAsync(i => i.ProductId == product.Id);
        Assert.Equal(0, inventory.Quantity);
    }

    [Fact]
    public async Task 新增商品_库存行缺失_详情库存按零计()
    {
        // 库存初始化已在上一用例覆盖；此用例验证读取侧对无库存行的商品按 0 处理
        var (context, handler, _) = CreateHandler();
        var category = await SeedCategoryAsync(context);

        var result = await handler.HandleAsync(new CreateProductRequest
        {
            Code = "sku-nostock",
            Name = "无库存商品",
            CategoryId = category.Id,
            Unit = "个",
            PurchasePrice = 0.1m,
            SalePrice = 0.2m,
            SafetyStock = 0,
        });

        var product = await context.Products.SingleAsync(p => p.Id.ToString() == result.Id);
        var row = await context.Inventory.SingleAsync(i => i.ProductId == product.Id);
        context.Inventory.Remove(row);
        await context.SaveChangesAsync();

        var detail = await new GetProductByIdRequestHandler(new ProductRepository(context))
            .HandleAsync(new GetProductByIdRequest { Id = product.Id });
        Assert.Equal(0, detail.StockQuantity);
    }

    [Fact]
    public async Task 新增商品_编码重复_应报ProductCodeExists()
    {
        var (context, handler, _) = CreateHandler();
        var category = await SeedCategoryAsync(context);

        await handler.HandleAsync(new CreateProductRequest
        {
            Code = "sku-dup",
            Name = "商品A",
            CategoryId = category.Id,
            Unit = "个",
        });

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateProductRequest
        {
            Code = "sku-dup",
            Name = "商品B",
            CategoryId = category.Id,
            Unit = "个",
        }));
        Assert.Equal(ErrorCode.ProductCodeExists, ex.Code);
    }

    [Fact]
    public async Task 新增商品_编码大小写不同_视为重复()
    {
        var (context, handler, _) = CreateHandler();
        var category = await SeedCategoryAsync(context);

        await handler.HandleAsync(new CreateProductRequest
        {
            Code = "SKU-CASE",
            Name = "商品A",
            CategoryId = category.Id,
            Unit = "个",
        });

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateProductRequest
        {
            Code = "sku-case",
            Name = "商品B",
            CategoryId = category.Id,
            Unit = "个",
        }));
        Assert.Equal(ErrorCode.ProductCodeExists, ex.Code);
    }

    [Fact]
    public async Task 新增商品_分类不存在_应报NotFound()
    {
        var (_, handler, _) = CreateHandler();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateProductRequest
        {
            Code = "sku-no-cat",
            Name = "商品A",
            CategoryId = Guid.NewGuid(),
            Unit = "个",
        }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 新增商品_审计字段应记录当前用户()
    {
        var (context, handler, user) = CreateHandler();
        var category = await SeedCategoryAsync(context);

        await handler.HandleAsync(new CreateProductRequest
        {
            Code = "sku-audit",
            Name = "审计商品",
            CategoryId = category.Id,
            Unit = "个",
        });

        var product = await context.Products.SingleAsync(p => p.Code == "sku-audit");
        Assert.Equal(user.Id, product.CreatedBy.ToString());
        Assert.Equal(user.Id, product.UpdatedBy.ToString());
    }
}
