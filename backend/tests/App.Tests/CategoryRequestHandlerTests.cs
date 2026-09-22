using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Categories.CreateCategory;
using App.Core.Features.Categories.DeleteCategory;
using App.Core.Features.Categories.GetCategories;
using App.Core.Features.Categories.GetCategoriesPaged;
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
        var handler = new CreateCategoryRequestHandler(new CategoryRepository(context), TestSupport.AuditLogger);

        var result = await handler.HandleAsync(new CreateCategoryRequest { Name = "原材料" });

        Assert.Equal("原材料", result.Name);
        Assert.True(await context.Categories.AnyAsync(c => c.Id.ToString() == result.Id));
    }

    [Fact]
    public async Task 新增分类_名称重复_应报CategoryNameExists()
    {
        var context = CreateContext();
        var handler = new CreateCategoryRequestHandler(new CategoryRepository(context), TestSupport.AuditLogger);
        await SeedCategoryAsync(context, "原材料");

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateCategoryRequest { Name = "原材料" }));
        Assert.Equal(ErrorCode.CategoryNameExists, ex.Code);
    }

    [Fact]
    public async Task 新增分类_名称大小写不同_视为重复()
    {
        var context = CreateContext();
        var handler = new CreateCategoryRequestHandler(new CategoryRepository(context), TestSupport.AuditLogger);
        await SeedCategoryAsync(context, "ABC");

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreateCategoryRequest { Name = "abc" }));
        Assert.Equal(ErrorCode.CategoryNameExists, ex.Code);
    }

    [Fact]
    public async Task 编辑分类_应改名且不影响其他()
    {
        var context = CreateContext();
        var handler = new UpdateCategoryRequestHandler(new CategoryRepository(context), TestSupport.AuditLogger);
        var category = await SeedCategoryAsync(context, "原材料");

        var result = await handler.HandleAsync(new UpdateCategoryRequest { Id = category.Id, Name = "辅料" });

        Assert.Equal("辅料", result.Name);
        Assert.Equal("辅料", (await context.Categories.SingleAsync(c => c.Id == category.Id)).Name);
    }

    [Fact]
    public async Task 编辑分类_名称与其他分类重复_应报CategoryNameExists()
    {
        var context = CreateContext();
        var handler = new UpdateCategoryRequestHandler(new CategoryRepository(context), TestSupport.AuditLogger);
        await SeedCategoryAsync(context, "原材料");
        var target = await SeedCategoryAsync(context, "包装材料");

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new UpdateCategoryRequest { Id = target.Id, Name = "原材料" }));
        Assert.Equal(ErrorCode.CategoryNameExists, ex.Code);
    }

    [Fact]
    public async Task 编辑分类_改为自身原名_应成功()
    {
        var context = CreateContext();
        var handler = new UpdateCategoryRequestHandler(new CategoryRepository(context), TestSupport.AuditLogger);
        var category = await SeedCategoryAsync(context, "原材料");

        var result = await handler.HandleAsync(new UpdateCategoryRequest { Id = category.Id, Name = "原材料" });

        Assert.Equal("原材料", result.Name);
    }

    [Fact]
    public async Task 删除分类_无引用_应成功()
    {
        var context = CreateContext();
        var handler = new DeleteCategoryRequestHandler(new CategoryRepository(context), TestSupport.AuditLogger);
        var category = await SeedCategoryAsync(context, "可删除");

        await handler.HandleAsync(new DeleteCategoryRequest { Id = category.Id });

        Assert.False(await context.Categories.AnyAsync(c => c.Id == category.Id));
    }

    [Fact]
    public async Task 删除分类_被商品引用_应报CategoryInUse()
    {
        var context = CreateContext();
        var handler = new DeleteCategoryRequestHandler(new CategoryRepository(context), TestSupport.AuditLogger);
        var category = await SeedCategoryAsync(context, "被引用");
        await SeedProductAsync(context, category);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new DeleteCategoryRequest { Id = category.Id }));
        Assert.Equal(ErrorCode.CategoryInUse, ex.Code);
    }

    [Fact]
    public async Task 删除分类_不存在_应报NotFound()
    {
        var context = CreateContext();
        var handler = new DeleteCategoryRequestHandler(new CategoryRepository(context), TestSupport.AuditLogger);

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

    [Fact]
    public async Task 分页查询_无关键词_应全量正序()
    {
        var context = CreateContext();
        var handler = new GetCategoriesPagedRequestHandler(new CategoryRepository(context));
        var second = await SeedCategoryAsync(context, "后建");
        var first = await SeedCategoryAsync(context, "先建");
        second.CreatedAt = DateTimeOffset.UtcNow.AddSeconds(1);
        await context.SaveChangesAsync();

        var result = await handler.HandleAsync(new GetCategoriesPagedRequest { Page = 1, PageSize = 20 });

        Assert.Equal(2, result.Total);
        Assert.Equal("先建", result.Items[0].Name);
        Assert.Equal("后建", result.Items[1].Name);
    }

    [Fact]
    public async Task 分页查询_关键词模糊匹配_应命中()
    {
        var context = CreateContext();
        var handler = new GetCategoriesPagedRequestHandler(new CategoryRepository(context));
        await SeedCategoryAsync(context, "原材料A");
        await SeedCategoryAsync(context, "原材料B");
        await SeedCategoryAsync(context, "包装材料");

        var result = await handler.HandleAsync(new GetCategoriesPagedRequest { Page = 1, PageSize = 20, Keyword = "原材料" });

        Assert.Equal(2, result.Total);
        Assert.All(result.Items, item => Assert.Contains("原材料", item.Name));
    }

    [Fact]
    public async Task 分页查询_分页切片_应正确()
    {
        var context = CreateContext();
        var handler = new GetCategoriesPagedRequestHandler(new CategoryRepository(context));
        var second = await SeedCategoryAsync(context, "二号");
        var third = await SeedCategoryAsync(context, "三号");
        var first = await SeedCategoryAsync(context, "一号");
        second.CreatedAt = DateTimeOffset.UtcNow;
        third.CreatedAt = DateTimeOffset.UtcNow.AddSeconds(1);
        first.CreatedAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await context.SaveChangesAsync();

        var page1 = await handler.HandleAsync(new GetCategoriesPagedRequest { Page = 1, PageSize = 2 });
        var page2 = await handler.HandleAsync(new GetCategoriesPagedRequest { Page = 2, PageSize = 2 });

        Assert.Equal(3, page1.Total);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal("一号", page1.Items[0].Name);
        Assert.Equal("二号", page1.Items[1].Name);
        Assert.Single(page2.Items);
        Assert.Equal("三号", page2.Items[0].Name);
    }
}
