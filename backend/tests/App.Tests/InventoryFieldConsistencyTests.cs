using App.Core.Entities;
using App.Core.Features.Inventory.GetInventory;
using App.Infrastructure;

namespace App.Tests;

/// <summary>
/// 库存查询字段约束一致性测试：保证查询关键词长度与数据库列长同源。
/// 守护点：
/// 1. keyword 长度不超过 Products 表 Code / Name 列长（跨用例一致性）；
/// 2. keyword 边界（50 通过 / 51 拒绝）与 ProductFieldConstraints.KeywordMaxLength 一致。
/// </summary>
public class InventoryFieldConsistencyTests
{
    private static int GetMaxLength<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetMaxLength()!.Value;

    [Fact]
    public void 关键词常量_应等于名称列长_即列长较大约束()
    {
        using var dbContext = TestSupport.CreateDbContext();

        // 常量约定：关键词长度对齐 Code / Name 列长的较大约束（Name 50 > Code 32）
        Assert.Equal(ProductFieldConstraints.KeywordMaxLength, GetMaxLength<Product>(dbContext, nameof(Product.Name)));
        Assert.True(ProductFieldConstraints.KeywordMaxLength >= GetMaxLength<Product>(dbContext, nameof(Product.Code)));
    }

    [Fact]
    public void 查询关键词长度_边界应通过_越界应拒绝()
    {
        var validator = new GetInventoryRequestValidator();
        var ok = new string('a', ProductFieldConstraints.KeywordMaxLength);
        var tooLong = new string('a', ProductFieldConstraints.KeywordMaxLength + 1);

        Assert.True(validator.Validate(new GetInventoryRequest { Page = 1, PageSize = 20, Keyword = ok }).IsValid);
        Assert.False(validator.Validate(new GetInventoryRequest { Page = 1, PageSize = 20, Keyword = tooLong }).IsValid);
    }

    [Fact]
    public void 分页参数_边界应通过_越界应拒绝()
    {
        var validator = new GetInventoryRequestValidator();

        Assert.True(validator.Validate(new GetInventoryRequest { Page = 1, PageSize = 1 }).IsValid);
        Assert.True(validator.Validate(new GetInventoryRequest { Page = 1, PageSize = 100 }).IsValid);
        Assert.False(validator.Validate(new GetInventoryRequest { Page = 0, PageSize = 20 }).IsValid);
        Assert.False(validator.Validate(new GetInventoryRequest { Page = 1, PageSize = 0 }).IsValid);
        Assert.False(validator.Validate(new GetInventoryRequest { Page = 1, PageSize = 101 }).IsValid);
    }
}
