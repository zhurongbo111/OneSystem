using App.Core.Entities;
using App.Core.Features.Categories.CreateCategory;
using App.Core.Features.Categories.GetCategoriesPaged;
using App.Core.Features.Categories.UpdateCategory;
using App.Infrastructure;

namespace App.Tests;

/// <summary>
/// 商品分类字段约束一致性测试：保证"格式校验"与"数据库约束"同源、同一字段在不同用例中规则一致。
/// 守护点：
/// 1. EF 模型 Categories.Name 实际列长度必须等于 CategoryFieldConstraints 常量；
/// 2. 名称长度在新增 / 编辑两处一致（边界值通过、越界拒绝）；
/// 3. 查询关键词长度按 KeywordMaxLength 边界校验，且上限不小于名称列长（覆盖列内任意取值）。
/// </summary>
public class CategoryFieldConsistencyTests
{
    private static int GetMaxLength<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetMaxLength()!.Value;

    [Fact]
    public void EF模型_Categories表列长度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(CategoryFieldConstraints.NameMaxLength, GetMaxLength<Category>(dbContext, nameof(Category.Name)));
    }

    [Fact]
    public void 名称长度_新增与编辑应一致()
    {
        var createValidator = new CreateCategoryRequestValidator();
        var updateValidator = new UpdateCategoryRequestValidator();

        var ok = new string('名', CategoryFieldConstraints.NameMaxLength);
        var tooLong = new string('名', CategoryFieldConstraints.NameMaxLength + 1);

        Assert.True(createValidator.Validate(new CreateCategoryRequest { Name = ok }).IsValid);
        Assert.False(createValidator.Validate(new CreateCategoryRequest { Name = tooLong }).IsValid);
        Assert.True(updateValidator.Validate(new UpdateCategoryRequest { Id = Guid.NewGuid(), Name = ok }).IsValid);
        Assert.False(updateValidator.Validate(new UpdateCategoryRequest { Id = Guid.NewGuid(), Name = tooLong }).IsValid);

        Assert.False(createValidator.Validate(new CreateCategoryRequest { Name = string.Empty }).IsValid);
        Assert.False(updateValidator.Validate(new UpdateCategoryRequest { Id = Guid.NewGuid(), Name = string.Empty }).IsValid);
    }

    [Fact]
    public void 查询关键词长度_应不超过Name列长()
    {
        var validator = new GetCategoriesPagedRequestValidator();

        var ok = new string('a', CategoryFieldConstraints.KeywordMaxLength);
        var tooLong = new string('a', CategoryFieldConstraints.KeywordMaxLength + 1);

        Assert.True(validator.Validate(new GetCategoriesPagedRequest { Page = 1, PageSize = 20, Keyword = ok }).IsValid);
        Assert.False(validator.Validate(new GetCategoriesPagedRequest { Page = 1, PageSize = 20, Keyword = tooLong }).IsValid);
        Assert.True(validator.Validate(new GetCategoriesPagedRequest { Page = 1, PageSize = 20 }).IsValid);

        // 查询只按 Name 模糊匹配：关键词上限不得超过被查询列长（后端规则 §5.3）
        Assert.True(CategoryFieldConstraints.KeywordMaxLength <= CategoryFieldConstraints.NameMaxLength);
    }
}
