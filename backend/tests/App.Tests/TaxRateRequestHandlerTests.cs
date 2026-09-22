using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.TaxRates.CreateTaxRate;
using App.Core.Features.TaxRates.DeleteTaxRate;
using App.Core.Features.TaxRates.GetTaxRateById;
using App.Core.Features.TaxRates.GetTaxRates;
using App.Core.Features.TaxRates.UpdateTaxRate;
using App.Core.Features.TaxRates.UpdateTaxRateStatus;
using App.Infrastructure;
using App.Infrastructure.Persistence;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// 税率用例处理器测试（列表筛选分页 / 新增 / 详情 / 编辑唯一性 / 删除 / 启停）
/// </summary>
public class TaxRateRequestHandlerTests
{
    private static Guid OperatorId { get; } = Guid.NewGuid();

    private static GetTaxRatesRequestHandler CreateGetTaxRatesHandler(AppDbContext dbContext)
        => new(new TaxRateRepository(dbContext));

    private static CreateTaxRateRequestHandler CreateCreateHandler(AppDbContext dbContext)
        => new(
            new TaxRateRepository(dbContext),
            new UnitOfWork(dbContext),
            new StubCurrentUser(OperatorId),
            TestSupport.AuditLogger);

    private static UpdateTaxRateRequestHandler CreateUpdateHandler(AppDbContext dbContext)
        => new(
            new TaxRateRepository(dbContext),
            new UnitOfWork(dbContext),
            new StubCurrentUser(OperatorId),
            TestSupport.AuditLogger);

    private static DeleteTaxRateRequestHandler CreateDeleteHandler(AppDbContext dbContext)
        => new(
            new TaxRateRepository(dbContext),
            new UnitOfWork(dbContext),
            TestSupport.AuditLogger);

    private static UpdateTaxRateStatusRequestHandler CreateStatusHandler(AppDbContext dbContext)
        => new(
            new TaxRateRepository(dbContext),
            new UnitOfWork(dbContext),
            new StubCurrentUser(OperatorId),
            TestSupport.AuditLogger);

    /// <summary>构建税率实体（默认启用）</summary>
    private static TaxRate NewTaxRate(
        string code = "VAT13",
        string name = "增值税 13%",
        decimal rate = 13m,
        TaxRateStatus status = TaxRateStatus.Enabled,
        string? remark = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new TaxRate
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Rate = rate,
            Status = status,
            Remark = remark,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    // ============================== 列表 ==============================

    [Fact]
    public async Task GetTaxRates_关键词与状态筛选_应分页返回()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        dbContext.TaxRates.AddRange(
            NewTaxRate("VAT13", "增值税 13%", 13m),
            NewTaxRate("VAT9", "增值税 9%", 9m),
            NewTaxRate("VAT6", "增值税 6%（停用）", 6m, TaxRateStatus.Disabled));
        await dbContext.SaveChangesAsync();

        var byKeyword = await CreateGetTaxRatesHandler(dbContext)
            .HandleAsync(new GetTaxRatesRequest { Keyword = "增值税", Page = 1, PageSize = 20 });
        Assert.Equal(3, byKeyword.Total);

        var byStatus = await CreateGetTaxRatesHandler(dbContext)
            .HandleAsync(new GetTaxRatesRequest { Status = (int)TaxRateStatus.Disabled, Page = 1, PageSize = 20 });
        Assert.Equal(1, byStatus.Total);
        Assert.Equal("VAT6", byStatus.Items[0].Code);

        var paged = await CreateGetTaxRatesHandler(dbContext)
            .HandleAsync(new GetTaxRatesRequest { Page = 2, PageSize = 2 });
        Assert.Equal(3, paged.Total);
        Assert.Single(paged.Items);
        Assert.Equal(2, paged.Page);
    }

    // ============================== 新增 ==============================

    [Fact]
    public async Task CreateTaxRate_合法请求_应落库并写审计字段()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var result = await CreateCreateHandler(dbContext).HandleAsync(new CreateTaxRateRequest
        {
            Code = " VAT13 ",
            Name = " 增值税 13% ",
            Rate = 13.5m,
            Status = (int)TaxRateStatus.Enabled,
            Remark = "  标准税率  ",
        });

        var saved = await dbContext.TaxRates.SingleAsync();
        Assert.Equal("VAT13", saved.Code);
        Assert.Equal("增值税 13%", saved.Name);
        Assert.Equal(13.5m, saved.Rate);
        Assert.Equal("标准税率", saved.Remark);
        Assert.Equal(OperatorId, saved.CreatedBy);
        Assert.Equal(saved.Id.ToString(), result.Id);
        Assert.Equal(13.5m, result.Rate);
    }

    [Fact]
    public async Task CreateTaxRate_备注纯空白_应按清空处理()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        await CreateCreateHandler(dbContext).HandleAsync(new CreateTaxRateRequest
        {
            Code = "VAT13",
            Name = "增值税 13%",
            Rate = 13m,
            Remark = "   ",
        });

        Assert.Null((await dbContext.TaxRates.SingleAsync()).Remark);
    }

    [Fact]
    public async Task CreateTaxRate_编码重复_应抛业务异常40151()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        dbContext.TaxRates.Add(NewTaxRate("vat13", "增值税 13%"));
        await dbContext.SaveChangesAsync();

        // 大小写不敏感：vat13 与 VAT13 视为重复
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(dbContext).HandleAsync(new CreateTaxRateRequest { Code = "VAT13", Name = "新税率", Rate = 13m }));

        Assert.Equal(ErrorCode.TaxRateCodeExists, ex.Code);
        Assert.Equal(1, await dbContext.TaxRates.CountAsync());
    }

    [Fact]
    public async Task CreateTaxRate_名称重复_应抛业务异常40152()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        dbContext.TaxRates.Add(NewTaxRate("VAT13", "增值税 13%"));
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(dbContext).HandleAsync(new CreateTaxRateRequest { Code = "VAT13B", Name = "增值税 13%", Rate = 13m }));

        Assert.Equal(ErrorCode.TaxRateNameExists, ex.Code);
    }

    // ============================== 详情 ==============================

    [Fact]
    public async Task GetTaxRateById_存在_应返回详情()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var taxRate = NewTaxRate();
        dbContext.TaxRates.Add(taxRate);
        await dbContext.SaveChangesAsync();

        var result = await new GetTaxRateByIdRequestHandler(new TaxRateRepository(dbContext))
            .HandleAsync(new GetTaxRateByIdRequest { Id = taxRate.Id });

        Assert.Equal("VAT13", result.Code);
        Assert.Equal(13m, result.Rate);
        Assert.Equal((int)TaxRateStatus.Enabled, result.Status);
    }

    [Fact]
    public async Task GetTaxRateById_不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => new GetTaxRateByIdRequestHandler(new TaxRateRepository(dbContext))
                .HandleAsync(new GetTaxRateByIdRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 编辑 ==============================

    [Fact]
    public async Task UpdateTaxRate_合法请求_应更新()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var taxRate = NewTaxRate();
        dbContext.TaxRates.Add(taxRate);
        await dbContext.SaveChangesAsync();

        var result = await CreateUpdateHandler(dbContext).HandleAsync(new UpdateTaxRateRequest
        {
            Id = taxRate.Id,
            Code = "VAT13",
            Name = "增值税 13%（改）",
            Rate = 13.0000m,
            Status = (int)TaxRateStatus.Disabled,
            Remark = "改了备注",
        });

        var saved = await dbContext.TaxRates.SingleAsync();
        Assert.Equal("增值税 13%（改）", saved.Name);
        Assert.Equal(13.0000m, saved.Rate);
        Assert.Equal(TaxRateStatus.Disabled, saved.Status);
        Assert.Equal(OperatorId, saved.UpdatedBy);
        Assert.Equal("增值税 13%（改）", result.Name);
    }

    [Fact]
    public async Task UpdateTaxRate_名称改为自身_应允许_改为他税率已有_应抛40152()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var taxRate = NewTaxRate("VAT13", "增值税 13%");
        dbContext.TaxRates.Add(taxRate);
        dbContext.TaxRates.Add(NewTaxRate("VAT9", "增值税 9%", 9m));
        await dbContext.SaveChangesAsync();

        // 自身名称不变：唯一性检查排除自身，应放行
        await CreateUpdateHandler(dbContext).HandleAsync(new UpdateTaxRateRequest
        {
            Id = taxRate.Id,
            Code = "VAT13",
            Name = "增值税 13%",
            Rate = 13m,
            Status = (int)TaxRateStatus.Enabled,
        });

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdateTaxRateRequest
            {
                Id = taxRate.Id,
                Code = "VAT13",
                Name = "增值税 9%",
                Rate = 13m,
                Status = (int)TaxRateStatus.Enabled,
            }));

        Assert.Equal(ErrorCode.TaxRateNameExists, ex.Code);
    }

    [Fact]
    public async Task UpdateTaxRate_编码与他税率重复_应抛业务异常40151()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var taxRate = NewTaxRate("VAT13", "增值税 13%");
        dbContext.TaxRates.Add(taxRate);
        dbContext.TaxRates.Add(NewTaxRate("VAT9", "增值税 9%", 9m));
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdateTaxRateRequest
            {
                Id = taxRate.Id,
                Code = "VAT9",
                Name = "增值税 13%",
                Rate = 13m,
                Status = (int)TaxRateStatus.Enabled,
            }));

        Assert.Equal(ErrorCode.TaxRateCodeExists, ex.Code);
    }

    [Fact]
    public async Task UpdateTaxRate_不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdateTaxRateRequest
            {
                Id = Guid.NewGuid(),
                Code = "VAT13",
                Name = "增值税 13%",
                Rate = 13m,
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 删除 ==============================

    [Fact]
    public async Task DeleteTaxRate_无引用_应删除()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var taxRate = NewTaxRate();
        dbContext.TaxRates.Add(taxRate);
        await dbContext.SaveChangesAsync();

        await CreateDeleteHandler(dbContext).HandleAsync(new DeleteTaxRateRequest { Id = taxRate.Id });

        Assert.Empty(await dbContext.TaxRates.ToListAsync());
    }

    [Fact]
    public async Task DeleteTaxRate_不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateDeleteHandler(dbContext).HandleAsync(new DeleteTaxRateRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 启停 ==============================

    [Fact]
    public async Task UpdateTaxRateStatus_停用_应更新状态()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var taxRate = NewTaxRate();
        dbContext.TaxRates.Add(taxRate);
        await dbContext.SaveChangesAsync();

        var result = await CreateStatusHandler(dbContext).HandleAsync(new UpdateTaxRateStatusRequest
        {
            Id = taxRate.Id,
            Status = (int)TaxRateStatus.Disabled,
        });

        Assert.Equal((int)TaxRateStatus.Disabled, result.Status);
        Assert.Equal(TaxRateStatus.Disabled, (await dbContext.TaxRates.SingleAsync()).Status);
    }

    [Fact]
    public async Task UpdateTaxRateStatus_不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateStatusHandler(dbContext).HandleAsync(new UpdateTaxRateStatusRequest
            {
                Id = Guid.NewGuid(),
                Status = (int)TaxRateStatus.Enabled,
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }
}