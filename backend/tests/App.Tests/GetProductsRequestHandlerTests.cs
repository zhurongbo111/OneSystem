using App.Core.Entities;
using App.Core.Features.Products.GetProducts;
using App.Infrastructure;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// GetProductsRequestHandler 测试：分页 / 关键词 / 分类 / 状态筛选 / 低库存标记
/// </summary>
public class GetProductsRequestHandlerTests
{
    private static (AppDbContext Context, GetProductsRequestHandler Handler) CreateHandler()
    {
        var context = TestSupport.CreateDbContext();
        var handler = new GetProductsRequestHandler(new ProductRepository(context));
        return (context, handler);
    }

    private static async Task SeedAsync(AppDbContext context, params (string Code, int Safety, int Stock)[] products)
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "原材料", CreatedAt = DateTimeOffset.UtcNow };
        context.Categories.Add(category);
        var baseTime = DateTimeOffset.UtcNow;
        var index = 0;
        foreach (var (code, safety, stock) in products)
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = $"商品{code}",
                CategoryId = category.Id,
                Unit = "个",
                SafetyStock = safety,
                Status = ProductStatus.Enabled,
                CreatedAt = baseTime.AddSeconds(index),
                UpdatedAt = baseTime.AddSeconds(index),
            };
            context.Products.Add(product);
            context.Inventory.Add(new Inventory { Id = Guid.NewGuid(), ProductId = product.Id, Quantity = stock, UpdatedAt = baseTime });
            index++;
        }

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task 分页_应返回正确条数与总数()
    {
        var (context, handler) = CreateHandler();
        await SeedAsync(context,
            ("a01", 0, 0), ("a02", 0, 0), ("a03", 0, 0), ("a04", 0, 0), ("a05", 0, 0));

        var result = await handler.HandleAsync(new GetProductsRequest { Page = 2, PageSize = 2 });

        Assert.Equal(5, result.Total);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task 关键词_编码或名称模糊匹配_应命中()
    {
        var (context, handler) = CreateHandler();
        await SeedAsync(context, ("bolt-01", 0, 0), ("nut-02", 0, 0));

        var result = await handler.HandleAsync(new GetProductsRequest { Page = 1, PageSize = 20, Keyword = "bolt" });

        Assert.Single(result.Items);
        Assert.Equal("bolt-01", result.Items[0].Code);
    }

    [Fact]
    public async Task 状态筛选_停用商品_应被过滤()
    {
        var (context, handler) = CreateHandler();
        await SeedAsync(context, ("p1", 0, 0), ("p2", 0, 0));
        var first = await context.Products.FirstAsync(p => p.Code == "p1");
        first.Status = ProductStatus.Disabled;
        await context.SaveChangesAsync();

        var result = await handler.HandleAsync(new GetProductsRequest
        {
            Page = 1,
            PageSize = 20,
            Status = ProductStatus.Enabled,
        });

        Assert.Single(result.Items);
        Assert.Equal("p2", result.Items[0].Code);
    }

    [Fact]
    public async Task 低库存标记_库存小于阈值且阈值大于零_应为真()
    {
        var (context, handler) = CreateHandler();
        await SeedAsync(context, ("low", 50, 10), ("ok", 50, 60), ("zero-threshold", 0, 10));

        var result = await handler.HandleAsync(new GetProductsRequest { Page = 1, PageSize = 20 });

        var low = result.Items.Single(i => i.Code == "low");
        var ok = result.Items.Single(i => i.Code == "ok");
        var zero = result.Items.Single(i => i.Code == "zero-threshold");
        Assert.True(low.IsBelowSafetyStock);
        Assert.False(ok.IsBelowSafetyStock);
        Assert.False(zero.IsBelowSafetyStock); // 阈值 0 不提醒
    }

    [Fact]
    public async Task 商品无库存行_仍返回且库存按零计()
    {
        // 040：按批次商品创建后在首次带批次入库前**无**库存行（不再预建 null 批次 0 行）。
        // 列表左连接必须容纳「无库存行」的商品并兜底 0。注意该缺陷在 InMemory 下不暴露
        // （真实库上会抛 "Nullable object must have a value" → 整页 50000），故另有 e2e 兜底。
        var (context, handler) = CreateHandler();
        var category = new Category { Id = Guid.NewGuid(), Name = "原材料", CreatedAt = DateTimeOffset.UtcNow };
        context.Categories.Add(category);
        var now = DateTimeOffset.UtcNow;
        var batch = new Product { Id = Guid.NewGuid(), Code = "batch-nostock", Name = "未入库批次商品", CategoryId = category.Id, Unit = "个", IsBatchManaged = true, Status = ProductStatus.Enabled, CreatedAt = now, UpdatedAt = now };
        var plain = new Product { Id = Guid.NewGuid(), Code = "plain-stock", Name = "有库存商品", CategoryId = category.Id, Unit = "个", Status = ProductStatus.Enabled, CreatedAt = now.AddSeconds(1), UpdatedAt = now };
        context.Products.Add(batch);
        context.Products.Add(plain);
        // 仅 plain 有库存行，batch 无任何库存行
        context.Inventory.Add(new Inventory { Id = Guid.NewGuid(), ProductId = plain.Id, Quantity = 6, UpdatedAt = now });
        await context.SaveChangesAsync();

        var result = await handler.HandleAsync(new GetProductsRequest { Page = 1, PageSize = 20 });

        Assert.Equal(2, result.Total);
        Assert.Equal(0, result.Items.Single(i => i.Code == "batch-nostock").StockQuantity);
        Assert.Equal(6, result.Items.Single(i => i.Code == "plain-stock").StockQuantity);
    }
}
