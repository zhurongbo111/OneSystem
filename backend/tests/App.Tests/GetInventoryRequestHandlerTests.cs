using App.Core.Entities;
using App.Core.Features.Inventory.GetInventory;
using App.Infrastructure;
using App.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// GetInventoryRequestHandler 测试：分页 / 关键词 / 分类筛选透传 / 仅启用商品 / 低库存映射三态
/// </summary>
public class GetInventoryRequestHandlerTests
{
    private static (AppDbContext Context, GetInventoryRequestHandler Handler) CreateHandler()
    {
        var context = TestSupport.CreateDbContext();
        var handler = new GetInventoryRequestHandler(new InventoryRepository(context));
        return (context, handler);
    }

    private static async Task<Guid> SeedCategoryAsync(AppDbContext context, string name)
    {
        var category = new Category { Id = Guid.NewGuid(), Name = name, CreatedAt = DateTimeOffset.UtcNow };
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category.Id;
    }

    private static Task SeedProductAsync(
        AppDbContext context,
        Guid categoryId,
        string code,
        int safety,
        int stock,
        ProductStatus status = ProductStatus.Enabled,
        DateTimeOffset? stockUpdatedAt = null)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = $"商品{code}",
            CategoryId = categoryId,
            Unit = "个",
            SafetyStock = safety,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        context.Products.Add(product);
        context.Inventory.Add(new Inventory
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Quantity = stock,
            UpdatedAt = stockUpdatedAt ?? DateTimeOffset.UtcNow,
        });
        return context.SaveChangesAsync();
    }

    [Fact]
    public async Task 分页_应返回正确条数与总数()
    {
        var (context, handler) = CreateHandler();
        var categoryId = await SeedCategoryAsync(context, "原材料");
        for (var i = 1; i <= 5; i++)
        {
            await SeedProductAsync(context, categoryId, $"code-{i:D2}", 0, 0);
        }

        var result = await handler.HandleAsync(new GetInventoryRequest { Page = 2, PageSize = 2 });

        Assert.Equal(5, result.Total);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task 按编码升序_应稳定排序()
    {
        var (context, handler) = CreateHandler();
        var categoryId = await SeedCategoryAsync(context, "原材料");
        await SeedProductAsync(context, categoryId, "b02", 0, 0);
        await SeedProductAsync(context, categoryId, "a01", 0, 0);
        await SeedProductAsync(context, categoryId, "c03", 0, 0);

        var result = await handler.HandleAsync(new GetInventoryRequest { Page = 1, PageSize = 20 });

        Assert.Equal(new[] { "a01", "b02", "c03" }, result.Items.Select(i => i.Code).ToArray());
    }

    [Fact]
    public async Task 关键词_编码或名称模糊匹配_应命中()
    {
        var (context, handler) = CreateHandler();
        var categoryId = await SeedCategoryAsync(context, "原材料");
        await SeedProductAsync(context, categoryId, "bolt-01", 0, 0);
        await SeedProductAsync(context, categoryId, "nut-02", 0, 0);

        var byCode = await handler.HandleAsync(new GetInventoryRequest { Page = 1, PageSize = 20, Keyword = "bolt" });
        Assert.Single(byCode.Items);
        Assert.Equal("bolt-01", byCode.Items[0].Code);

        // 名称由 SeedProductAsync 按 "商品<code>" 生成，验证名称维度同样命中
        var byName = await handler.HandleAsync(new GetInventoryRequest { Page = 1, PageSize = 20, Keyword = "nut-02" });
        Assert.Single(byName.Items);
        Assert.Equal("nut-02", byName.Items[0].Code);
    }

    [Fact]
    public async Task 分类筛选_应只返回该分类商品()
    {
        var (context, handler) = CreateHandler();
        var rawId = await SeedCategoryAsync(context, "原材料");
        var pkgId = await SeedCategoryAsync(context, "包装物");
        await SeedProductAsync(context, rawId, "raw-01", 0, 0);
        await SeedProductAsync(context, pkgId, "pkg-01", 0, 0);

        var result = await handler.HandleAsync(new GetInventoryRequest { Page = 1, PageSize = 20, CategoryId = rawId });

        Assert.Single(result.Items);
        Assert.Equal("raw-01", result.Items[0].Code);
        Assert.Equal("原材料", result.Items[0].CategoryName);
    }

    [Fact]
    public async Task 停用商品_即使库存非零_应被过滤()
    {
        var (context, handler) = CreateHandler();
        var categoryId = await SeedCategoryAsync(context, "原材料");
        await SeedProductAsync(context, categoryId, "disabled-01", 0, 99, ProductStatus.Disabled);
        await SeedProductAsync(context, categoryId, "enabled-01", 0, 0);

        var result = await handler.HandleAsync(new GetInventoryRequest { Page = 1, PageSize = 20 });

        Assert.Single(result.Items);
        Assert.Equal("enabled-01", result.Items[0].Code);
    }

    [Fact]
    public async Task 低库存映射_三态_应正确()
    {
        var (context, handler) = CreateHandler();
        var categoryId = await SeedCategoryAsync(context, "原材料");
        // 阈值 0 → 不提醒；库存 = 阈值 → 不提醒；阈值 > 0 且库存 < 阈值（含库存 0）→ 提醒
        await SeedProductAsync(context, categoryId, "zero-threshold", 0, 10);
        await SeedProductAsync(context, categoryId, "equal", 5, 5);
        await SeedProductAsync(context, categoryId, "below", 5, 3);
        await SeedProductAsync(context, categoryId, "empty", 5, 0);

        var result = await handler.HandleAsync(new GetInventoryRequest { Page = 1, PageSize = 20 });

        Assert.False(result.Items.Single(i => i.Code == "zero-threshold").IsBelowSafetyStock);
        Assert.False(result.Items.Single(i => i.Code == "equal").IsBelowSafetyStock);
        Assert.True(result.Items.Single(i => i.Code == "below").IsBelowSafetyStock);
        Assert.True(result.Items.Single(i => i.Code == "empty").IsBelowSafetyStock);
    }

    [Fact]
    public async Task 联查带出_单位_阈值_变动时间_应透传()
    {
        var (context, handler) = CreateHandler();
        var categoryId = await SeedCategoryAsync(context, "原材料");
        var updatedAt = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
        await SeedProductAsync(context, categoryId, "inv-01", 10, 4, stockUpdatedAt: updatedAt);

        var result = await handler.HandleAsync(new GetInventoryRequest { Page = 1, PageSize = 20 });

        var item = Assert.Single(result.Items);
        Assert.Equal("个", item.Unit);
        Assert.Equal(10, item.SafetyStock);
        Assert.Equal(4, item.StockQuantity);
        Assert.Equal(updatedAt, item.UpdatedAt);
    }
}
