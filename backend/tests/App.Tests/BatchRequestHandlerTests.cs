using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Batches.CreateBatch;
using App.Core.Features.Batches.GetBatchById;
using App.Core.Features.Batches.GetBatchPickList;
using App.Core.Features.Batches.GetBatches;
using App.Core.Features.Batches.UpdateBatch;
using App.Core.Features.Batches.UpdateBatchStatus;
using App.Infrastructure;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// 批次档案用例测试（040 §3.4）：新增 / 编辑 / 启停 / 详情 / 分页 / 开单下拉；
/// 新增约束：商品不存在 40400、未启用批次管理 40000、批次号同商品唯一（大小写不敏感）40129；
/// 开单下拉按 FEFO（到期日升序、无到期日最后）并计算 IsExpired / IsNearExpiry（固定「今天」）。
/// </summary>
public class BatchRequestHandlerTests
{
    private static AppDbContext CreateContext() => TestSupport.CreateDbContext();

    private static Guid UserGuid { get; } = Guid.NewGuid();

    /// <summary>固定「今天」（2026-09-30，UTC）：近效期窗口 ≤ 2026-10-30</summary>
    private static DateTimeOffset Today { get; } = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    /// <summary>构造日期（默认 0 点；可带时分秒用于「日期粒度抹零」验证输入）</summary>
    private static DateTimeOffset Midnight(int year, int month, int day, int hour = 0, int minute = 0, int second = 0, TimeSpan offset = default)
        => new(year, month, day, hour, minute, second, offset);

    /// <summary>断言某时刻在本地日历为给定日期且时间为 0 点（Handler 按 <c>DateTimeOffset.Date</c> 落库，时间归零在其自身偏移内）</summary>
    private static bool IsDateOnlyAt(DateTimeOffset value, int year, int month, int day)
        => value.Hour == 0 && value.Minute == 0 && value.Second == 0
            && value.LocalDateTime.Year == year
            && value.LocalDateTime.Month == month
            && value.LocalDateTime.Day == day;

    // ---------- 新增批次 ----------

    [Fact]
    public async Task 新增批次_应成功且日期按日期粒度存储()
    {
        var product = new Product { Id = Guid.NewGuid(), Code = "SKU-B01", Name = "批次商品", IsBatchManaged = true, Status = ProductStatus.Enabled };
        var productRepo = new FakeProductRepository();
        productRepo.ById[product.Id] = product;
        var batchRepo = new FakeBatchRepository();
        var handler = new CreateBatchRequestHandler(batchRepo, productRepo, new StubCurrentUser(UserGuid), TestSupport.AuditLogger);

        var result = await handler.HandleAsync(new CreateBatchRequest
        {
            ProductId = product.Id,
            BatchNo = "B20260101",
            ProductionDate = Midnight(2026, 1, 1, 15, 30, 45),
            ExpiryDate = Midnight(2026, 10, 1, 8, 0, 0, TimeSpan.Zero),
            Remark = "  首批  ",
        });

        Assert.Equal("B20260101", result.BatchNo);
        Assert.Equal("SKU-B01", result.ProductCode);
        Assert.Equal((int)PartnerStatus.Enabled, result.Status);
        // 日期粒度：时间部分被抹零，日历日保持（按本地日历日 + 时间归零断言，避免偏移差异）
        Assert.True(IsDateOnlyAt(result.ProductionDate!.Value, 2026, 1, 1));
        Assert.True(IsDateOnlyAt(result.ExpiryDate!.Value, 2026, 10, 1));
        // 备注去空白
        Assert.Equal("首批", result.Remark);
        Assert.Single(batchRepo.Added);
    }

    [Fact]
    public async Task 新增批次_商品不存在_应报NotFound()
    {
        var handler = new CreateBatchRequestHandler(
            new FakeBatchRepository(), new FakeProductRepository(), new StubCurrentUser(UserGuid), TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new CreateBatchRequest { ProductId = Guid.NewGuid(), BatchNo = "B1" }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task 新增批次_商品未启用批次管理_应报Validation()
    {
        var product = new Product { Id = Guid.NewGuid(), Code = "SKU-NB", Name = "非批次商品", IsBatchManaged = false, Status = ProductStatus.Enabled };
        var productRepo = new FakeProductRepository();
        productRepo.ById[product.Id] = product;
        var handler = new CreateBatchRequestHandler(
            new FakeBatchRepository(), productRepo, new StubCurrentUser(UserGuid), TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new CreateBatchRequest { ProductId = product.Id, BatchNo = "B1" }));

        Assert.Equal(ErrorCode.Validation, ex.Code);
    }

    [Fact]
    public async Task 新增批次_同商品批次号重复_大小写不同_应报BatchNoExists()
    {
        var product = new Product { Id = Guid.NewGuid(), Code = "SKU-B02", Name = "批次商品2", IsBatchManaged = true, Status = ProductStatus.Enabled };
        var productRepo = new FakeProductRepository();
        productRepo.ById[product.Id] = product;
        var batchRepo = new FakeBatchRepository();
        batchRepo.Seed(product.Id, "B20260101");
        var handler = new CreateBatchRequestHandler(batchRepo, productRepo, new StubCurrentUser(UserGuid), TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new CreateBatchRequest { ProductId = product.Id, BatchNo = "b20260101" }));

        Assert.Equal(ErrorCode.BatchNoExists, ex.Code);
    }

    [Fact]
    public async Task 新增批次_不同商品同批次号_应成功()
    {
        var p1 = new Product { Id = Guid.NewGuid(), Code = "SKU-P1", Name = "商品1", IsBatchManaged = true, Status = ProductStatus.Enabled };
        var p2 = new Product { Id = Guid.NewGuid(), Code = "SKU-P2", Name = "商品2", IsBatchManaged = true, Status = ProductStatus.Enabled };
        var productRepo = new FakeProductRepository();
        productRepo.ById[p1.Id] = p1;
        productRepo.ById[p2.Id] = p2;
        var batchRepo = new FakeBatchRepository();
        batchRepo.Seed(p1.Id, "SHARED");
        var handler = new CreateBatchRequestHandler(batchRepo, productRepo, new StubCurrentUser(UserGuid), TestSupport.AuditLogger);

        // 批次号唯一是「同商品内」，跨商品不冲突
        var result = await handler.HandleAsync(new CreateBatchRequest { ProductId = p2.Id, BatchNo = "SHARED" });

        Assert.Equal("SHARED", result.BatchNo);
    }

    // ---------- 编辑批次 ----------

    [Fact]
    public async Task 编辑批次_应全量覆盖且批次号不可改()
    {
        var product = new Product { Id = Guid.NewGuid(), Code = "SKU-B03", Name = "批次商品3", IsBatchManaged = true, Status = ProductStatus.Enabled };
        var productRepo = new FakeProductRepository();
        productRepo.ById[product.Id] = product;
        var batchRepo = new FakeBatchRepository();
        batchRepo.Seed(product.Id, "B20260101", productionDate: Midnight(2026, 1, 1), expiryDate: Midnight(2026, 10, 1));
        var handler = new UpdateBatchRequestHandler(batchRepo, productRepo, new StubCurrentUser(UserGuid), TestSupport.AuditLogger);
        var id = batchRepo.IdOf(product.Id, "B20260101")!.Value;

        var result = await handler.HandleAsync(new UpdateBatchRequest
        {
            Id = id,
            ProductionDate = Midnight(2026, 2, 1, 9, 0, 0, TimeSpan.Zero),
            ExpiryDate = null,
            Remark = "   ",
        });

        Assert.Equal("B20260101", result.BatchNo); // 批次号保持
        Assert.True(IsDateOnlyAt(result.ProductionDate!.Value, 2026, 2, 1));
        Assert.Null(result.ExpiryDate); // 到期日清空
        Assert.Null(result.Remark); // 纯空白视为清空
    }

    [Fact]
    public async Task 编辑批次_批次不存在_应报NotFound()
    {
        var handler = new UpdateBatchRequestHandler(
            new FakeBatchRepository(), new FakeProductRepository(), new StubCurrentUser(UserGuid), TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new UpdateBatchRequest { Id = Guid.NewGuid(), Remark = "x" }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ---------- 启停批次 ----------

    [Fact]
    public async Task 停用批次_应置为停用()
    {
        var product = new Product { Id = Guid.NewGuid(), Code = "SKU-B04", Name = "批次商品4", IsBatchManaged = true, Status = ProductStatus.Enabled };
        var productRepo = new FakeProductRepository();
        productRepo.ById[product.Id] = product;
        var batchRepo = new FakeBatchRepository();
        batchRepo.Seed(product.Id, "B20260101");
        var handler = new UpdateBatchStatusRequestHandler(batchRepo, productRepo, new StubCurrentUser(UserGuid), TestSupport.AuditLogger);
        var id = batchRepo.IdOf(product.Id, "B20260101")!.Value;

        var result = await handler.HandleAsync(new UpdateBatchStatusRequest { Id = id, Status = (int)PartnerStatus.Disabled });

        Assert.Equal((int)PartnerStatus.Disabled, result.Status);
    }

    [Fact]
    public async Task 启停批次_批次不存在_应报NotFound()
    {
        var handler = new UpdateBatchStatusRequestHandler(
            new FakeBatchRepository(), new FakeProductRepository(), new StubCurrentUser(UserGuid), TestSupport.AuditLogger);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new UpdateBatchStatusRequest { Id = Guid.NewGuid(), Status = (int)PartnerStatus.Disabled }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ---------- 批次详情 ----------

    [Fact]
    public async Task 查询批次详情_应联查商品编码名称()
    {
        var product = new Product { Id = Guid.NewGuid(), Code = "SKU-B05", Name = "批次商品5", IsBatchManaged = true, Status = ProductStatus.Enabled };
        var productRepo = new FakeProductRepository();
        productRepo.ById[product.Id] = product;
        var batchRepo = new FakeBatchRepository();
        batchRepo.Seed(product.Id, "B20260101", expiryDate: Midnight(2026, 10, 1));
        var handler = new GetBatchByIdRequestHandler(batchRepo, productRepo);
        var id = batchRepo.IdOf(product.Id, "B20260101")!.Value;

        var result = await handler.HandleAsync(new GetBatchByIdRequest { Id = id });

        Assert.Equal("SKU-B05", result.ProductCode);
        Assert.Equal("批次商品5", result.ProductName);
        Assert.Equal("B20260101", result.BatchNo);
    }

    [Fact]
    public async Task 查询批次详情_不存在_应报NotFound()
    {
        var handler = new GetBatchByIdRequestHandler(new FakeBatchRepository(), new FakeProductRepository());

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new GetBatchByIdRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ---------- 分页查询（真实仓储 + InMemory） ----------

    [Fact]
    public async Task 分页查询_近效期筛选_应仅命中窗口内批次()
    {
        var context = CreateContext();
        var product = SeedBatchProductAsync(context);
        await SeedBatchAsync(context, product.Id, "NEAR", Midnight(2026, 10, 10)); // 10 天 → 窗口内
        await SeedBatchAsync(context, product.Id, "FAR", Midnight(2027, 1, 31)); // 超出窗口
        await SeedBatchAsync(context, product.Id, "NOEXP", null); // 永不过期
        var handler = new GetBatchesRequestHandler(new BatchRepository(context), new TestClock(Today));

        var result = await handler.HandleAsync(new GetBatchesRequest { OnlyExpiring = true, Page = 1, PageSize = 20 });

        Assert.Equal(1, result.Total);
        Assert.Equal("NEAR", result.Items[0].BatchNo);
    }

    [Fact]
    public async Task 分页查询_关键词模糊_应命中()
    {
        var context = CreateContext();
        var product = SeedBatchProductAsync(context);
        await SeedBatchAsync(context, product.Id, "BA1");
        await SeedBatchAsync(context, product.Id, "BA2");
        await SeedBatchAsync(context, product.Id, "ZZ9");
        var handler = new GetBatchesRequestHandler(new BatchRepository(context), new TestClock(Today));

        var result = await handler.HandleAsync(new GetBatchesRequest { Keyword = "ba", Page = 1, PageSize = 20 });

        Assert.Equal(2, result.Total);
        Assert.All(result.Items, i => Assert.StartsWith("BA", i.BatchNo));
    }

    [Fact]
    public async Task 分页查询_关键词按商品编码或名称_应命中()
    {
        // 搜索框占位「批次号 / 商品编码 / 商品名称」：keyword 须同时匹配商品编码 / 名称（040 §3），
        // 而非仅批次号；批次号不含关键词时仍应按商品命中。
        var context = CreateContext();
        var product = SeedBatchProductAsync(context); // 编码 SKU-BQ / 名称 批次查询商品
        await SeedBatchAsync(context, product.Id, "ZZ1");
        await SeedBatchAsync(context, product.Id, "ZZ2");
        var handler = new GetBatchesRequestHandler(new BatchRepository(context), new TestClock(Today));

        // 批次号 ZZ* 不含 sku，按商品编码命中
        var byCode = await handler.HandleAsync(new GetBatchesRequest { Keyword = "sku-bq", Page = 1, PageSize = 20 });
        Assert.Equal(2, byCode.Total);
        // 按商品名称命中
        var byName = await handler.HandleAsync(new GetBatchesRequest { Keyword = "批次查询商品", Page = 1, PageSize = 20 });
        Assert.Equal(2, byName.Total);
        // 无关关键词不命中
        var none = await handler.HandleAsync(new GetBatchesRequest { Keyword = "no-such-keyword", Page = 1, PageSize = 20 });
        Assert.Equal(0, none.Total);
    }

    [Fact]
    public async Task 分页查询_库存合计_应联查各仓批次行()
    {
        var context = CreateContext();
        var warehouse = TestSupport.SeedDefaultWarehouse(context);
        var product = SeedBatchProductAsync(context);
        var batch = await SeedBatchAsync(context, product.Id, "STOCK");
        context.Inventory.Add(new Inventory { ProductId = product.Id, WarehouseId = warehouse.Id, BatchId = batch.Id, Quantity = 10, SafetyStock = 0, UpdatedAt = Today });
        context.Inventory.Add(new Inventory { ProductId = product.Id, WarehouseId = Guid.NewGuid(), BatchId = batch.Id, Quantity = 5, SafetyStock = 0, UpdatedAt = Today });
        await context.SaveChangesAsync();
        var handler = new GetBatchesRequestHandler(new BatchRepository(context), new TestClock(Today));

        var result = await handler.HandleAsync(new GetBatchesRequest { Page = 1, PageSize = 20 });

        Assert.Equal(15, result.Items[0].TotalStock);
    }

    // ---------- 开单下拉（真实仓储 + InMemory） ----------

    [Fact]
    public async Task 开单下拉_应按FEFO排序并计算过期近效期标记()
    {
        var context = CreateContext();
        var warehouse = TestSupport.SeedDefaultWarehouse(context);
        var product = SeedBatchProductAsync(context);
        var near = await SeedBatchAsync(context, product.Id, "NEAR", Midnight(2026, 10, 5)); // 未过期、近效期
        var noexp = await SeedBatchAsync(context, product.Id, "NOEXP", null); // 永不过期
        var expired = await SeedBatchAsync(context, product.Id, "EXPD", Midnight(2026, 9, 1)); // 已过期但有库存 → 保留
        context.Inventory.Add(new Inventory { ProductId = product.Id, WarehouseId = warehouse.Id, BatchId = near.Id, Quantity = 3, SafetyStock = 0, UpdatedAt = Today });
        context.Inventory.Add(new Inventory { ProductId = product.Id, WarehouseId = warehouse.Id, BatchId = noexp.Id, Quantity = 5, SafetyStock = 0, UpdatedAt = Today });
        context.Inventory.Add(new Inventory { ProductId = product.Id, WarehouseId = warehouse.Id, BatchId = expired.Id, Quantity = 2, SafetyStock = 0, UpdatedAt = Today });
        await context.SaveChangesAsync();
        var handler = new GetBatchPickListRequestHandler(new BatchRepository(context), new TestClock(Today));

        var result = await handler.HandleAsync(new GetBatchPickListRequest { ProductId = product.Id, WarehouseId = warehouse.Id });

        // FEFO：有到期日在前按到期日升序（EXPD 09-01 < NEAR 10-05），无到期日最后
        Assert.Equal(["EXPD", "NEAR", "NOEXP"], result.Select(i => i.BatchNo).ToArray());
        var expd = result.Single(i => i.BatchNo == "EXPD");
        var nearItem = result.Single(i => i.BatchNo == "NEAR");
        var noexpItem = result.Single(i => i.BatchNo == "NOEXP");
        Assert.True(expd.IsExpired);
        Assert.False(expd.IsNearExpiry);
        Assert.Equal(2, expd.AvailableQuantity);
        Assert.False(nearItem.IsExpired);
        Assert.True(nearItem.IsNearExpiry);
        Assert.Equal(3, nearItem.AvailableQuantity);
        Assert.False(noexpItem.IsExpired);
        Assert.False(noexpItem.IsNearExpiry);
    }

    [Fact]
    public async Task 开单下拉_已过期且无库存_应被过滤()
    {
        var context = CreateContext();
        var warehouse = TestSupport.SeedDefaultWarehouse(context);
        var product = SeedBatchProductAsync(context);
        await SeedBatchAsync(context, product.Id, "EXPD", Midnight(2026, 9, 1)); // 过期 + 无库存
        await SeedBatchAsync(context, product.Id, "NEAR", Midnight(2026, 10, 5)); // 近效期
        var handler = new GetBatchPickListRequestHandler(new BatchRepository(context), new TestClock(Today));

        var result = await handler.HandleAsync(new GetBatchPickListRequest { ProductId = product.Id, WarehouseId = warehouse.Id });

        Assert.Single(result);
        Assert.Equal("NEAR", result[0].BatchNo);
    }

    // ---------- 辅助 ----------

    private static Product SeedBatchProductAsync(AppDbContext context)
    {
        var product = TestSupport.NewProduct("SKU-BQ", "批次查询商品", 0m, ProductStatus.Enabled, Guid.NewGuid(), "个");
        product.IsBatchManaged = true;
        context.Products.Add(product);
        context.SaveChanges();
        return product;
    }

    private static Task<Batch> SeedBatchAsync(AppDbContext context, Guid productId, string batchNo, DateTimeOffset? expiryDate = null)
    {
        var batch = new Batch
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            BatchNo = batchNo,
            ExpiryDate = expiryDate,
            Status = PartnerStatus.Enabled,
            CreatedAt = Today,
            UpdatedAt = Today,
        };
        context.Batches.Add(batch);
        context.SaveChanges();
        return Task.FromResult(batch);
    }
}
