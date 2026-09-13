using App.Core.Entities;
using App.Core.Features.Categories.CreateCategory;
using App.Core.Features.Categories.UpdateCategory;
using App.Core.Features.Products.CreateProduct;
using App.Core.Features.Products.GetProducts;
using App.Core.Features.Products.UpdateProduct;
using App.Infrastructure;

namespace App.Tests;

/// <summary>
/// 商品 / 分类字段约束一致性测试：保证"格式校验"与"数据库约束"同源、同一字段在不同用例中规则一致。
/// 守护点：
/// 1. EF 模型实际列长度 / 精度必须等于 ProductFieldConstraints / CategoryFieldConstraints 常量；
/// 2. 采购价 / 销售价区间在新增 / 编辑两处一致；
/// 3. 安全库存区间在新增 / 编辑两处一致；
/// 4. 编码格式 / 长度、名称 / 单位 / 备注长度按常量约束校验；
/// 5. 查询关键词长度不超过对应列长度。
/// </summary>
public class ProductFieldConsistencyTests
{
    private static int GetMaxLength<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetMaxLength()!.Value;

    private static int GetPrecision<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetPrecision()!.Value;

    private static int GetScale<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetScale()!.Value;

    [Fact]
    public void EF模型_Products表列长度与精度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(ProductFieldConstraints.CodeMaxLength, GetMaxLength<Product>(dbContext, nameof(Product.Code)));
        Assert.Equal(ProductFieldConstraints.NameMaxLength, GetMaxLength<Product>(dbContext, nameof(Product.Name)));
        Assert.Equal(ProductFieldConstraints.UnitMaxLength, GetMaxLength<Product>(dbContext, nameof(Product.Unit)));
        Assert.Equal(ProductFieldConstraints.RemarkMaxLength, GetMaxLength<Product>(dbContext, nameof(Product.Remark)));
        Assert.Equal(18, GetPrecision<Product>(dbContext, nameof(Product.PurchasePrice)));
        Assert.Equal(2, GetScale<Product>(dbContext, nameof(Product.PurchasePrice)));
        Assert.Equal(18, GetPrecision<Product>(dbContext, nameof(Product.SalePrice)));
        Assert.Equal(2, GetScale<Product>(dbContext, nameof(Product.SalePrice)));
    }

    [Fact]
    public void EF模型_Categories表列长度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(CategoryFieldConstraints.NameMaxLength, GetMaxLength<Category>(dbContext, nameof(Category.Name)));
    }

    [Fact]
    public void 价格区间_新增与编辑应一致()
    {
        var lower = ProductFieldConstraints.PriceMinValue - 0.01m;
        var upper = ProductFieldConstraints.PriceMaxValue + 0.01m;

        Assert.False(new CreateProductRequestValidator()
            .Validate(new CreateProductRequest { Code = "code", Name = "名称", CategoryId = Guid.NewGuid(), Unit = "个", PurchasePrice = lower }).IsValid);
        Assert.False(new UpdateProductRequestValidator()
            .Validate(new UpdateProductRequest { Id = Guid.NewGuid(), Name = "名称", CategoryId = Guid.NewGuid(), Unit = "个", PurchasePrice = lower }).IsValid);

        Assert.True(new CreateProductRequestValidator()
            .Validate(new CreateProductRequest { Code = "code", Name = "名称", CategoryId = Guid.NewGuid(), Unit = "个", PurchasePrice = ProductFieldConstraints.PriceMinValue }).IsValid);
        Assert.True(new UpdateProductRequestValidator()
            .Validate(new UpdateProductRequest { Id = Guid.NewGuid(), Name = "名称", CategoryId = Guid.NewGuid(), Unit = "个", PurchasePrice = ProductFieldConstraints.PriceMinValue }).IsValid);

        Assert.False(new CreateProductRequestValidator()
            .Validate(new CreateProductRequest { Code = "code", Name = "名称", CategoryId = Guid.NewGuid(), Unit = "个", SalePrice = upper }).IsValid);
        Assert.False(new UpdateProductRequestValidator()
            .Validate(new UpdateProductRequest { Id = Guid.NewGuid(), Name = "名称", CategoryId = Guid.NewGuid(), Unit = "个", SalePrice = upper }).IsValid);
    }

    [Fact]
    public void 安全库存区间_新增与编辑应一致()
    {
        var below = ProductFieldConstraints.SafetyStockMinValue - 1;
        var above = ProductFieldConstraints.SafetyStockMaxValue + 1;

        Assert.False(new CreateProductRequestValidator()
            .Validate(new CreateProductRequest { Code = "code", Name = "名称", CategoryId = Guid.NewGuid(), Unit = "个", SafetyStock = below }).IsValid);
        Assert.False(new UpdateProductRequestValidator()
            .Validate(new UpdateProductRequest { Id = Guid.NewGuid(), Name = "名称", CategoryId = Guid.NewGuid(), Unit = "个", SafetyStock = below }).IsValid);

        Assert.True(new CreateProductRequestValidator()
            .Validate(new CreateProductRequest { Code = "code", Name = "名称", CategoryId = Guid.NewGuid(), Unit = "个", SafetyStock = ProductFieldConstraints.SafetyStockMaxValue }).IsValid);
        Assert.True(new UpdateProductRequestValidator()
            .Validate(new UpdateProductRequest { Id = Guid.NewGuid(), Name = "名称", CategoryId = Guid.NewGuid(), Unit = "个", SafetyStock = ProductFieldConstraints.SafetyStockMaxValue }).IsValid);

        Assert.False(new CreateProductRequestValidator()
            .Validate(new CreateProductRequest { Code = "code", Name = "名称", CategoryId = Guid.NewGuid(), Unit = "个", SafetyStock = above }).IsValid);
        Assert.False(new UpdateProductRequestValidator()
            .Validate(new UpdateProductRequest { Id = Guid.NewGuid(), Name = "名称", CategoryId = Guid.NewGuid(), Unit = "个", SafetyStock = above }).IsValid);
    }

    [Fact]
    public void 编码_长度与字符集_应按常量约束校验()
    {
        var validator = new CreateProductRequestValidator();

        Assert.True(ValidateCode(validator, new string('a', ProductFieldConstraints.CodeMaxLength)));
        Assert.False(ValidateCode(validator, new string('a', ProductFieldConstraints.CodeMaxLength + 1)));
        Assert.False(ValidateCode(validator, new string('a', ProductFieldConstraints.CodeMinLength - 1)));
        Assert.False(ValidateCode(validator, "中文编码"));
        Assert.True(ValidateCode(validator, "ABC-123_x"));
    }

    [Fact]
    public void 名称_单位_备注长度_应按常量约束校验()
    {
        var createValidator = new CreateProductRequestValidator();

        var nameOk = new string('名', ProductFieldConstraints.NameMaxLength);
        var nameLong = new string('名', ProductFieldConstraints.NameMaxLength + 1);
        Assert.True(ValidateBase(createValidator, nameOk));
        Assert.False(ValidateBase(createValidator, nameLong));

        var unitLong = new string('u', ProductFieldConstraints.UnitMaxLength + 1);
        var invalid = new CreateProductRequest
        {
            Code = "code",
            Name = "名称",
            CategoryId = Guid.NewGuid(),
            Unit = unitLong,
        };
        Assert.False(createValidator.Validate(invalid).IsValid);

        var remarkLong = new string('r', ProductFieldConstraints.RemarkMaxLength + 1);
        var invalidRemark = new CreateProductRequest
        {
            Code = "code",
            Name = "名称",
            CategoryId = Guid.NewGuid(),
            Unit = "个",
            Remark = remarkLong,
        };
        Assert.False(createValidator.Validate(invalidRemark).IsValid);
    }

    [Fact]
    public void 分类名称长度_新增与编辑应一致且不超过列长度()
    {
        var ok = new string('分', CategoryFieldConstraints.NameMaxLength);
        var tooLong = new string('分', CategoryFieldConstraints.NameMaxLength + 1);

        Assert.True(new CreateCategoryRequestValidator().Validate(new CreateCategoryRequest { Name = ok }).IsValid);
        Assert.True(new UpdateCategoryRequestValidator().Validate(new UpdateCategoryRequest { Id = Guid.NewGuid(), Name = ok }).IsValid);
        Assert.False(new CreateCategoryRequestValidator().Validate(new CreateCategoryRequest { Name = tooLong }).IsValid);
        Assert.False(new UpdateCategoryRequestValidator().Validate(new UpdateCategoryRequest { Id = Guid.NewGuid(), Name = tooLong }).IsValid);
    }

    [Fact]
    public void 查询关键词长度_应不超过对应列长度()
    {
        var ok = new string('a', ProductFieldConstraints.KeywordMaxLength);
        var tooLong = new string('a', ProductFieldConstraints.KeywordMaxLength + 1);

        Assert.True(new GetProductsRequestValidator().Validate(new GetProductsRequest { Page = 1, PageSize = 20, Keyword = ok }).IsValid);
        Assert.False(new GetProductsRequestValidator().Validate(new GetProductsRequest { Page = 1, PageSize = 20, Keyword = tooLong }).IsValid);
    }

    private static bool ValidateCode(CreateProductRequestValidator validator, string code)
        => validator.Validate(new CreateProductRequest
        {
            Code = code,
            Name = "名称",
            CategoryId = Guid.NewGuid(),
            Unit = "个",
        }).IsValid;

    private static bool ValidateBase(CreateProductRequestValidator validator, string name)
        => validator.Validate(new CreateProductRequest
        {
            Code = "code",
            Name = name,
            CategoryId = Guid.NewGuid(),
            Unit = "个",
        }).IsValid;
}
