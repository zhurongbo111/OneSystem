using App.Core.Entities;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 库存台账仓储测试（InMemory 范围）：初始化 + 读取。
/// 说明：IncrementAsync / TryDecrementAsync 基于 EF Core ExecuteUpdate（条件更新）实现数据库级原子性，
/// 依赖关系型提供程序的行锁，EF InMemory 不支持 ExecuteUpdate；其并发正确性由 erp-purchase / erp-sale
/// 阶段在真实 PostgreSQL 上以集成测试覆盖（见 design.md §5.2）。
/// </summary>
public class InventoryRepositoryTests
{
    private static async Task<Guid> SeedProductWithStockAsync(AppDbContext context, int quantity)
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "原材料", CreatedAt = DateTimeOffset.UtcNow };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Code = "sku-inv",
            Name = "库存商品",
            CategoryId = category.Id,
            Unit = "个",
            Status = ProductStatus.Enabled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        context.Categories.Add(category);
        context.Products.Add(product);
        context.Inventory.Add(new Inventory { Id = Guid.NewGuid(), ProductId = product.Id, Quantity = quantity, UpdatedAt = DateTimeOffset.UtcNow });
        await context.SaveChangesAsync();
        return product.Id;
    }

    [Fact]
    public async Task 初始化库存行后_读取应返回设定值()
    {
        var context = TestSupport.CreateDbContext();
        var repo = new InventoryRepository(context);
        var productId = await SeedProductWithStockAsync(context, 42);

        var quantity = await repo.GetQuantityAsync(productId);

        Assert.Equal(42, quantity);
    }

    [Fact]
    public async Task 无库存行的商品_读取应返回零()
    {
        var context = TestSupport.CreateDbContext();
        var repo = new InventoryRepository(context);
        var category = new Category { Id = Guid.NewGuid(), Name = "原材料", CreatedAt = DateTimeOffset.UtcNow };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Code = "sku-noinv",
            Name = "无库存行",
            CategoryId = category.Id,
            Unit = "个",
            Status = ProductStatus.Enabled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        context.Categories.Add(category);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var quantity = await repo.GetQuantityAsync(product.Id);

        Assert.Equal(0, quantity);
    }
}
