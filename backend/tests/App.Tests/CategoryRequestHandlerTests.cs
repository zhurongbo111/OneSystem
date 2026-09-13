using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Categories.CreateCategory;
using App.Core.Features.Categories.DeleteCategory;
using App.Core.Features.Categories.GetCategories;
using App.Core.Features.Categories.UpdateCategory;
using App.Infrastructure;
using App.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// 商品分类用例测试：新增 / 编辑 / 删除 / 列表；名称唯一（大小写不敏感）；被引用不可删除
/// </summary>
public class CategoryRequestHandlerTests
{
    private static AppDbContext CreateContext() => TestSupport.CreateDbContext();

    private static async Task<Category> SeedCategoryAsync(AppDbContext context, string name)
    {
        var category = new Category { Id = Guid.NewGuid(), Name = name, CreatedAt = DateTimeOffset.UtcNow };
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category;
    }

    private static async Task<Product> SeedProductAsync(AppDbContext context, Category category)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Code = "sku-cat",
            Name = "引用商品",
            CategoryId = category.Id,
            Unit = "个",
            Status = ProductStatus.Enabled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    [Fact]
    public async Task 新增分类_应成功()
    {
        var context = CreateContext();
        var handler = new CreateCategoryRequestHandler(new CategoryRepository(context));

        var result = await handler.HandleAsync(new CreateCategoryRequest { Name = "原材料" });

        Assert.Equal("原材料", result.Name);
        Assert.True(await context.Categories.AnyAsync(c => c.Id.ToString() == result.Id));
    }

    [Fact]
    public async Task 新增分类_名称重复_应报CategoryNameExists()
    {
        var context = CreateContext();
        var handler = new CreateCategoryRequestHandler(new CategoryRepository(context));
        await SeedCategoryAsync(context, "原材料");

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateCategoryRequest { Name = "原材料" }));
        Assert.Equal(ErrorCode.CategoryNameExists, ex.Code);
    }

    [Fact]
    public async Task 新增分类_名称大小写不同_视为重复()
    {
        var context = CreateContext();
        var handler = new CreateCategoryRequestHandler(new CategoryRepository(context));
        await SeedCategoryAsync(context, "ABC");

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateCategoryRequest { Name = "abc" }));
        Assert.Equal(ErrorCode.CategoryNameExists, ex.Code);
    }

    [Fact]
    public async Task 编辑分类_应改名且不影响其他()
    {
        var context = CreateContext();
        var handler = new UpdateCategoryRequestHandler(new CategoryRepository(context));
        var category = await SeedCategoryAsync(context, "原材料");

        var result = await handler.HandleAsync(new UpdateCategoryRequest { Id = category.Id, Name = "辅料" });

        Assert.Equal("辅料", result.Name);
        Assert.Equal("辅料", (await context.Categories.SingleAsync(c => c.Id == category.Id)).Name);
    }

    [Fact]
    public async Task 编辑分类_名称与其他分类重复_应报CategoryNameExists()
    {
        var context = CreateContext();
        var handler = new UpdateCategoryRequestHandler(new CategoryRepository(context));
        await SeedCategoryAsync(context, "原材料");
        var target = await SeedCategoryAsync(context, "包装材料");

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdateCategoryRequest { Id = target.Id, Name = "原材料" }));
        Assert.Equal(ErrorCode.CategoryNameExists, ex.Code);
    }

    [Fact]
    public async Task 编辑分类_改为自身原名_应成功()
    {
        var context = CreateContext();
        var handler = new UpdateCategoryRequestHandler(new CategoryRepository(context));
        var category = await SeedCategoryAsync(context, "原材料");

        var result = await handler.HandleAsync(new UpdateCategoryRequest { Id = category.Id, Name = "原材料" });

        Assert.Equal("原材料", result.Name);
    }

    [Fact]
    public async Task 删除分类_无引用_应成功()
    {
        var context = CreateContext();
        var handler = new DeleteCategoryRequestHandler(new CategoryRepository(context));
        var category = await SeedCategoryAsync(context, "可删除");

        await handler.HandleAsync(new DeleteCategoryRequest { Id = category.Id });

        Assert.False(await context.Categories.AnyAsync(c => c.Id == category.Id));
    }

    [Fact]
    public async Task 删除分类_被商品引用_应报CategoryInUse()
    {
        var context = CreateContext();
        var handler = new DeleteCategoryRequestHandler(new CategoryRepository(context));
        var category = await SeedCategoryAsync(context, "被引用");
        await SeedProductAsync(context, category);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new DeleteCategoryRequest { Id = category.Id }));
        Assert.Equal(ErrorCode.CategoryInUse, ex.Code);
    }

    [Fact]
    public async Task 删除分类_不存在_应报NotFound()
    {
        var context = CreateContext();
        var handler = new DeleteCategoryRequestHandler(new CategoryRepository(context));

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new DeleteCategoryRequest { Id = Guid.NewGuid() }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 列表_应按创建时间正序()
    {
        var context = CreateContext();
        var handler = new GetCategoriesRequestHandler(new CategoryRepository(context));
        var second = await SeedCategoryAsync(context, "后建");
        var first = await SeedCategoryAsync(context, "先建");
        second.CreatedAt = DateTimeOffset.UtcNow.AddSeconds(1);
        await context.SaveChangesAsync();

        var result = await handler.HandleAsync(new GetCategoriesRequest());

        Assert.Equal(2, result.Count);
        Assert.Equal("先建", result[0].Name);
        Assert.Equal("后建", result[1].Name);
    }
}
