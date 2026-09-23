using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.PartnerPrices.CreatePartnerPrice;
using App.Core.Features.PartnerPrices.DeletePartnerPrice;
using App.Core.Features.PartnerPrices.GetEffectivePrices;
using App.Core.Features.PartnerPrices.GetPartnerPriceById;
using App.Core.Features.PartnerPrices.GetPartnerPrices;
using App.Core.Features.PartnerPrices.UpdatePartnerPrice;
using App.Infrastructure;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 客户价用例测试：新增 / 编辑 / 删除 / 详情 / 列表的客户与商品校验（含不可改字段）、重复校验与出参映射；
/// 批量取价的优先级（协议价 > 商品销售价）与来源标注；取价明细见 design.md §0.1。
/// 客户 / 商品走真实仓储 + InMemory（普通读写，不依赖关系型特性）；协议价走 <see cref="FakePartnerPriceRepository"/>。
/// </summary>
public class PartnerPriceRequestHandlerTests
{
    private static AppDbContext CreateContext() => TestSupport.CreateDbContext();

    private static async Task<Partner> SeedPartnerAsync(
        AppDbContext context,
        string name = "客户甲",
        PartnerType type = PartnerType.Customer,
        PartnerStatus status = PartnerStatus.Enabled)
    {
        var partner = TestSupport.NewPartner(name, type, status);
        context.Partners.Add(partner);
        await context.SaveChangesAsync();
        return partner;
    }

    private static async Task<Product> SeedProductAsync(
        AppDbContext context,
        string code = "sku-001",
        string name = "测试商品",
        decimal salePrice = 100m,
        ProductStatus status = ProductStatus.Enabled)
    {
        var product = TestSupport.NewProduct(code, name, status: status);
        product.SalePrice = salePrice;
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    private static CreatePartnerPriceRequestHandler CreateCreateHandler(
        AppDbContext context, FakePartnerPriceRepository prices, RecordingAuditLogger auditLogger, Guid operatorId)
        => new(prices, new PartnerRepository(context), new ProductRepository(context),
            new StubCurrentUser(operatorId), auditLogger);

    private static UpdatePartnerPriceRequestHandler CreateUpdateHandler(
        AppDbContext context, FakePartnerPriceRepository prices, RecordingAuditLogger auditLogger, Guid operatorId)
        => new(prices, new PartnerRepository(context), new ProductRepository(context),
            new StubCurrentUser(operatorId), auditLogger);

    private static PartnerPrice NewPartnerPrice(Guid partnerId, Guid productId, decimal price = 95m, string? remark = "旧备注")
    {
        var now = DateTimeOffset.UtcNow;
        return new PartnerPrice
        {
            Id = Guid.NewGuid(),
            PartnerId = partnerId,
            ProductId = productId,
            Price = price,
            Remark = remark,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    [Fact]
    public async Task 新增协议价_应带出联查字段并写入审计()
    {
        var context = CreateContext();
        var operatorId = Guid.NewGuid();
        var prices = new FakePartnerPriceRepository();
        var auditLogger = new RecordingAuditLogger();
        var partner = await SeedPartnerAsync(context);
        var product = await SeedProductAsync(context, salePrice: 120m);
        var handler = CreateCreateHandler(context, prices, auditLogger, operatorId);

        var result = await handler.HandleAsync(new CreatePartnerPriceRequest
        {
            PartnerId = partner.Id,
            ProductId = product.Id,
            Price = 95m,
            Remark = "  年度协议  ",
        });

        Assert.Equal(95m, result.Price);
        Assert.Equal("客户甲", result.PartnerName);
        Assert.Equal("sku-001", result.ProductCode);
        Assert.Equal("测试商品", result.ProductName);
        Assert.Equal("个", result.Unit);
        Assert.Equal(120m, result.SalePrice);
        Assert.Equal("年度协议", result.Remark);

        var entity = prices.Get(Guid.Parse(result.Id));
        Assert.Equal(partner.Id, entity.PartnerId);
        Assert.Equal(product.Id, entity.ProductId);
        Assert.Equal(operatorId, entity.CreatedBy);

        var entry = Assert.Single(auditLogger.Entries);
        Assert.Equal(AuditResource.PartnerPrice, entry.Resource);
        Assert.Equal(AuditAction.Create, entry.Action);
    }

    [Fact]
    public async Task 新增协议价_备注为空白_应存为空()
    {
        var context = CreateContext();
        var prices = new FakePartnerPriceRepository();
        var partner = await SeedPartnerAsync(context);
        var product = await SeedProductAsync(context);
        var handler = CreateCreateHandler(context, prices, new RecordingAuditLogger(), Guid.NewGuid());

        var result = await handler.HandleAsync(new CreatePartnerPriceRequest
        {
            PartnerId = partner.Id,
            ProductId = product.Id,
            Price = 1m,
            Remark = "   ",
        });

        Assert.Null(result.Remark);
        Assert.Null(prices.Get(Guid.Parse(result.Id)).Remark);
    }

    [Fact]
    public async Task 新增协议价_客户不存在_应报NotFound()
    {
        var context = CreateContext();
        var prices = new FakePartnerPriceRepository();
        var product = await SeedProductAsync(context);
        var handler = CreateCreateHandler(context, prices, new RecordingAuditLogger(), Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreatePartnerPriceRequest
        {
            PartnerId = Guid.NewGuid(),
            ProductId = product.Id,
            Price = 10m,
        }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 新增协议价_客户停用_应报PartnerDisabled()
    {
        var context = CreateContext();
        var prices = new FakePartnerPriceRepository();
        var partner = await SeedPartnerAsync(context, status: PartnerStatus.Disabled);
        var product = await SeedProductAsync(context);
        var handler = CreateCreateHandler(context, prices, new RecordingAuditLogger(), Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreatePartnerPriceRequest
        {
            PartnerId = partner.Id,
            ProductId = product.Id,
            Price = 10m,
        }));

        Assert.Equal(ErrorCode.PartnerDisabled, ex.Code);
    }

    [Fact]
    public async Task 新增协议价_供应商_应报Validation()
    {
        var context = CreateContext();
        var prices = new FakePartnerPriceRepository();
        var supplier = await SeedPartnerAsync(context, name: "供应商甲", type: PartnerType.Supplier);
        var product = await SeedProductAsync(context);
        var handler = CreateCreateHandler(context, prices, new RecordingAuditLogger(), Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreatePartnerPriceRequest
        {
            PartnerId = supplier.Id,
            ProductId = product.Id,
            Price = 10m,
        }));

        Assert.Equal(ErrorCode.Validation, ex.Code);
    }

    [Fact]
    public async Task 新增协议价_商品不存在_应报NotFound()
    {
        var context = CreateContext();
        var prices = new FakePartnerPriceRepository();
        var partner = await SeedPartnerAsync(context);
        var handler = CreateCreateHandler(context, prices, new RecordingAuditLogger(), Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreatePartnerPriceRequest
        {
            PartnerId = partner.Id,
            ProductId = Guid.NewGuid(),
            Price = 10m,
        }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 新增协议价_商品停用_应报ProductDisabled()
    {
        var context = CreateContext();
        var prices = new FakePartnerPriceRepository();
        var partner = await SeedPartnerAsync(context);
        var product = await SeedProductAsync(context, status: ProductStatus.Disabled);
        var handler = CreateCreateHandler(context, prices, new RecordingAuditLogger(), Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreatePartnerPriceRequest
        {
            PartnerId = partner.Id,
            ProductId = product.Id,
            Price = 10m,
        }));

        Assert.Equal(ErrorCode.ProductDisabled, ex.Code);
    }

    [Fact]
    public async Task 新增协议价_客户与商品重复_应报PartnerPriceExists()
    {
        var context = CreateContext();
        var prices = new FakePartnerPriceRepository();
        var partner = await SeedPartnerAsync(context);
        var product = await SeedProductAsync(context);
        prices.Seed(NewPartnerPrice(partner.Id, product.Id));
        var handler = CreateCreateHandler(context, prices, new RecordingAuditLogger(), Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new CreatePartnerPriceRequest
        {
            PartnerId = partner.Id,
            ProductId = product.Id,
            Price = 10m,
        }));

        Assert.Equal(ErrorCode.PartnerPriceExists, ex.Code);
    }

    [Fact]
    public async Task 编辑协议价_应改价与备注且客户商品不变()
    {
        var context = CreateContext();
        var operatorId = Guid.NewGuid();
        var prices = new FakePartnerPriceRepository();
        var auditLogger = new RecordingAuditLogger();
        var partner = await SeedPartnerAsync(context);
        var product = await SeedProductAsync(context, salePrice: 120m);
        var entity = NewPartnerPrice(partner.Id, product.Id);
        prices.Seed(entity);
        var handler = CreateUpdateHandler(context, prices, auditLogger, operatorId);

        var result = await handler.HandleAsync(new UpdatePartnerPriceRequest
        {
            Id = entity.Id,
            Price = 88m,
            Remark = "新年度协议",
        });

        Assert.Equal(88m, result.Price);
        Assert.Equal(partner.Id.ToString(), result.PartnerId);
        Assert.Equal(product.Id.ToString(), result.ProductId);
        Assert.Equal(120m, result.SalePrice);

        var stored = prices.Get(entity.Id);
        Assert.Equal(partner.Id, stored.PartnerId);
        Assert.Equal(product.Id, stored.ProductId);
        Assert.Equal("新年度协议", stored.Remark);
        Assert.Equal(operatorId, stored.UpdatedBy);

        Assert.Equal(AuditAction.Update, Assert.Single(auditLogger.Entries).Action);
    }

    [Fact]
    public async Task 编辑协议价_备注空串_应按全量覆盖清空()
    {
        var context = CreateContext();
        var prices = new FakePartnerPriceRepository();
        var partner = await SeedPartnerAsync(context);
        var product = await SeedProductAsync(context);
        var entity = NewPartnerPrice(partner.Id, product.Id);
        prices.Seed(entity);
        var handler = CreateUpdateHandler(context, prices, new RecordingAuditLogger(), Guid.NewGuid());

        var result = await handler.HandleAsync(new UpdatePartnerPriceRequest { Id = entity.Id, Price = 88m, Remark = string.Empty });

        Assert.Null(result.Remark);
        Assert.Null(prices.Get(entity.Id).Remark);
    }

    [Fact]
    public async Task 编辑协议价_不存在_应报NotFound()
    {
        var context = CreateContext();
        var prices = new FakePartnerPriceRepository();
        var handler = CreateUpdateHandler(context, prices, new RecordingAuditLogger(), Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            new UpdatePartnerPriceRequest { Id = Guid.NewGuid(), Price = 10m }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
        Assert.Equal(0, prices.UpdateCount);
    }

    [Fact]
    public async Task 删除协议价_应移除记录并写入审计()
    {
        var context = CreateContext();
        var prices = new FakePartnerPriceRepository();
        var auditLogger = new RecordingAuditLogger();
        var partner = await SeedPartnerAsync(context);
        var product = await SeedProductAsync(context);
        var entity = NewPartnerPrice(partner.Id, product.Id);
        prices.Seed(entity);
        var handler = new DeletePartnerPriceRequestHandler(
            prices, new PartnerRepository(context), new ProductRepository(context), auditLogger);

        var result = await handler.HandleAsync(new DeletePartnerPriceRequest { Id = entity.Id });

        Assert.Null(result);
        Assert.Equal([entity.Id], prices.DeletedIds);
        var entry = Assert.Single(auditLogger.Entries);
        Assert.Equal(AuditResource.PartnerPrice, entry.Resource);
        Assert.Equal(AuditAction.Delete, entry.Action);
    }

    [Fact]
    public async Task 删除协议价_不存在_应报NotFound()
    {
        var context = CreateContext();
        var prices = new FakePartnerPriceRepository();
        var handler = new DeletePartnerPriceRequestHandler(
            prices, new PartnerRepository(context), new ProductRepository(context), new RecordingAuditLogger());

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            handler.HandleAsync(new DeletePartnerPriceRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
        Assert.Empty(prices.DeletedIds);
    }

    [Fact]
    public async Task 查询协议价详情_应返回客户与商品联查字段()
    {
        var context = CreateContext();
        var prices = new FakePartnerPriceRepository();
        var partner = await SeedPartnerAsync(context);
        var product = await SeedProductAsync(context, salePrice: 120m);
        var entity = NewPartnerPrice(partner.Id, product.Id);
        prices.Seed(entity);
        var handler = new GetPartnerPriceByIdRequestHandler(
            prices, new PartnerRepository(context), new ProductRepository(context));

        var result = await handler.HandleAsync(new GetPartnerPriceByIdRequest { Id = entity.Id });

        Assert.Equal(entity.Id.ToString(), result.Id);
        Assert.Equal("客户甲", result.PartnerName);
        Assert.Equal("sku-001", result.ProductCode);
        Assert.Equal(120m, result.SalePrice);
        Assert.Equal(entity.CreatedAt, result.CreatedAt);
    }

    [Fact]
    public async Task 查询协议价详情_不存在_应报NotFound()
    {
        var context = CreateContext();
        var prices = new FakePartnerPriceRepository();
        var handler = new GetPartnerPriceByIdRequestHandler(
            prices, new PartnerRepository(context), new ProductRepository(context));

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            new GetPartnerPriceByIdRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 查询协议价详情_关联客户缺失_应报NotFound()
    {
        var context = CreateContext();
        var prices = new FakePartnerPriceRepository();
        var product = await SeedProductAsync(context);
        var entity = NewPartnerPrice(Guid.NewGuid(), product.Id);
        prices.Seed(entity);
        var handler = new GetPartnerPriceByIdRequestHandler(
            prices, new PartnerRepository(context), new ProductRepository(context));

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(
            new GetPartnerPriceByIdRequest { Id = entity.Id }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 分页查询_应透传筛选并入参映射含销售价对比()
    {
        var prices = new FakePartnerPriceRepository();
        var partnerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        prices.PagedItems =
        [
            new PartnerPriceListItem
            {
                Id = Guid.NewGuid(),
                PartnerId = partnerId,
                PartnerName = "客户甲",
                ProductId = productId,
                ProductCode = "sku-001",
                ProductName = "测试商品",
                Unit = "箱",
                Price = 95m,
                SalePrice = 120m,
                Remark = "年度协议",
                CreatedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            },
        ];
        prices.PagedTotal = 1;
        var handler = new GetPartnerPricesRequestHandler(prices);

        var result = await handler.HandleAsync(new GetPartnerPricesRequest
        {
            PartnerId = partnerId,
            ProductId = productId,
            Keyword = "客户",
            Page = 2,
            PageSize = 10,
        });

        var query = Assert.Single(prices.PagedQueries);
        Assert.Equal(partnerId, query.PartnerId);
        Assert.Equal(productId, query.ProductId);
        Assert.Equal("客户", query.Keyword);
        Assert.Equal(2, query.Page);
        Assert.Equal(10, query.PageSize);

        Assert.Equal(1, result.Total);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        var item = Assert.Single(result.Items);
        Assert.Equal("客户甲", item.PartnerName);
        Assert.Equal("sku-001", item.ProductCode);
        Assert.Equal("箱", item.Unit);
        Assert.Equal(95m, item.Price);
        Assert.Equal(120m, item.SalePrice);
        Assert.Equal("年度协议", item.Remark);
    }

    [Fact]
    public async Task 取价_有协议价_应返回协议价且来源为协议价()
    {
        var context = CreateContext();
        var prices = new FakePartnerPriceRepository();
        var partner = await SeedPartnerAsync(context);
        var product = await SeedProductAsync(context, salePrice: 120m);
        prices.Seed(NewPartnerPrice(partner.Id, product.Id, price: 95m));
        prices.ProductSalePrices[product.Id] = 120m;
        var handler = new GetEffectivePricesRequestHandler(prices, new ProductRepository(context));

        var result = await handler.HandleAsync(new GetEffectivePricesRequest
        {
            PartnerId = partner.Id,
            ProductIds = [product.Id],
        });

        var item = Assert.Single(result);
        Assert.Equal(product.Id.ToString(), item.ProductId);
        Assert.Equal(95m, item.UnitPrice);
        Assert.Equal((int)PriceSource.Agreement, item.Source);
    }

    [Fact]
    public async Task 取价_无协议价_应返回商品销售价且来源为默认价()
    {
        var context = CreateContext();
        var prices = new FakePartnerPriceRepository();
        var partner = await SeedPartnerAsync(context);
        var product = await SeedProductAsync(context, salePrice: 120m);
        prices.ProductSalePrices[product.Id] = 120m;
        var handler = new GetEffectivePricesRequestHandler(prices, new ProductRepository(context));

        var result = await handler.HandleAsync(new GetEffectivePricesRequest
        {
            PartnerId = partner.Id,
            ProductIds = [product.Id],
        });

        var item = Assert.Single(result);
        Assert.Equal(120m, item.UnitPrice);
        Assert.Equal((int)PriceSource.Default, item.Source);
    }

    [Fact]
    public async Task 取价_协议价等于销售价_应仍算协议价()
    {
        var context = CreateContext();
        var prices = new FakePartnerPriceRepository();
        var partner = await SeedPartnerAsync(context);
        var product = await SeedProductAsync(context, salePrice: 100m);
        prices.Seed(NewPartnerPrice(partner.Id, product.Id, price: 100m));
        prices.ProductSalePrices[product.Id] = 100m;
        var handler = new GetEffectivePricesRequestHandler(prices, new ProductRepository(context));

        var result = await handler.HandleAsync(new GetEffectivePricesRequest
        {
            PartnerId = partner.Id,
            ProductIds = [product.Id],
        });

        Assert.Equal((int)PriceSource.Agreement, Assert.Single(result).Source);
    }

    [Fact]
    public async Task 取价_商品id重复_应去重后查询一次()
    {
        var context = CreateContext();
        var prices = new FakePartnerPriceRepository();
        var partner = await SeedPartnerAsync(context);
        var product = await SeedProductAsync(context, salePrice: 120m);
        prices.ProductSalePrices[product.Id] = 120m;
        var handler = new GetEffectivePricesRequestHandler(prices, new ProductRepository(context));

        var result = await handler.HandleAsync(new GetEffectivePricesRequest
        {
            PartnerId = partner.Id,
            ProductIds = [product.Id, product.Id],
        });

        Assert.Single(result);
        Assert.Single(Assert.Single(prices.EffectiveQueries).ProductIds);
    }

    [Fact]
    public async Task 取价_商品不存在_应报NotFound()
    {
        var context = CreateContext();
        var prices = new FakePartnerPriceRepository();
        var partner = await SeedPartnerAsync(context);
        var handler = new GetEffectivePricesRequestHandler(prices, new ProductRepository(context));

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new GetEffectivePricesRequest
        {
            PartnerId = partner.Id,
            ProductIds = [Guid.NewGuid()],
        }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
        Assert.Empty(prices.EffectiveQueries);
    }
}
