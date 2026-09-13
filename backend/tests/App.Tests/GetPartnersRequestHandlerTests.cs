using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Partners.GetPartnerById;
using App.Core.Features.Partners.GetPartners;
using App.Infrastructure;
using App.Infrastructure.Persistence;
using App.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// GetPartnersRequestHandler / GetPartnerByIdRequestHandler 测试：
/// 分页 / 关键词（名称 / 联系人）/ 类型 / 状态筛选 / 详情 / 不存在
/// </summary>
public class GetPartnersRequestHandlerTests
{
    private static (AppDbContext Context, GetPartnersRequestHandler Handler) CreateHandler()
    {
        var context = TestSupport.CreateDbContext();
        var handler = new GetPartnersRequestHandler(new PartnerRepository(context));
        return (context, handler);
    }

    private static async Task<Guid> SeedAsync(
        AppDbContext context,
        string name,
        PartnerType type,
        PartnerStatus status = PartnerStatus.Enabled,
        string? contact = null,
        int offsetSeconds = 0)
    {
        var id = Guid.NewGuid();
        var baseTime = DateTimeOffset.UtcNow.AddSeconds(offsetSeconds);
        context.Partners.Add(new Partner
        {
            Id = id,
            Name = name,
            Type = type,
            Contact = contact,
            Status = status,
            CreatedAt = baseTime,
            UpdatedAt = baseTime,
        });
        await context.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task 分页_应返回正确条数与总数()
    {
        var (context, handler) = CreateHandler();
        await SeedAsync(context, "供应商1", PartnerType.Supplier, offsetSeconds: 1);
        await SeedAsync(context, "供应商2", PartnerType.Supplier, offsetSeconds: 2);
        await SeedAsync(context, "客户1", PartnerType.Customer, offsetSeconds: 3);
        await SeedAsync(context, "客户2", PartnerType.Customer, offsetSeconds: 4);
        await SeedAsync(context, "两者1", PartnerType.Both, offsetSeconds: 5);

        var result = await handler.HandleAsync(new GetPartnersRequest { Page = 2, PageSize = 2 });

        Assert.Equal(5, result.Total);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task 关键词_名称或联系人模糊匹配_应命中()
    {
        var (context, handler) = CreateHandler();
        await SeedAsync(context, "杭州电子", PartnerType.Supplier, contact: "李四");
        await SeedAsync(context, "上海机械", PartnerType.Customer, contact: "杭州王五");

        var byName = await handler.HandleAsync(new GetPartnersRequest { Page = 1, PageSize = 20, Keyword = "杭州" });
        Assert.Equal(2, byName.Items.Count); // 名称命中 + 联系人命中

        var byContact = await handler.HandleAsync(new GetPartnersRequest { Page = 1, PageSize = 20, Keyword = "李四" });
        Assert.Single(byContact.Items);
        Assert.Equal("杭州电子", byContact.Items[0].Name);
    }

    [Fact]
    public async Task 类型筛选_供应商_应过滤客户与两者()
    {
        var (context, handler) = CreateHandler();
        await SeedAsync(context, "供应商A", PartnerType.Supplier);
        await SeedAsync(context, "客户A", PartnerType.Customer);
        await SeedAsync(context, "两者A", PartnerType.Both);

        var result = await handler.HandleAsync(new GetPartnersRequest
        {
            Page = 1,
            PageSize = 20,
            Type = PartnerType.Supplier,
        });

        Assert.Single(result.Items);
        Assert.Equal("供应商A", result.Items[0].Name);
    }

    [Fact]
    public async Task 状态筛选_停用_应过滤启用项()
    {
        var (context, handler) = CreateHandler();
        await SeedAsync(context, "启用项", PartnerType.Supplier, PartnerStatus.Enabled);
        await SeedAsync(context, "停用项", PartnerType.Supplier, PartnerStatus.Disabled);

        var result = await handler.HandleAsync(new GetPartnersRequest
        {
            Page = 1,
            PageSize = 20,
            Status = PartnerStatus.Disabled,
        });

        Assert.Single(result.Items);
        Assert.Equal("停用项", result.Items[0].Name);
    }

    [Fact]
    public async Task 详情_应返回对应字段()
    {
        var (context, handler) = CreateHandler();
        var id = await SeedAsync(context, "详情供应商", PartnerType.Both, contact: "赵六", offsetSeconds: 1);

        var detail = await new GetPartnerByIdRequestHandler(new PartnerRepository(context))
            .HandleAsync(new GetPartnerByIdRequest { Id = id });

        Assert.Equal("详情供应商", detail.Name);
        Assert.Equal((int)PartnerType.Both, detail.Type);
        Assert.Equal("赵六", detail.Contact);
    }

    [Fact]
    public async Task 详情_不存在_应报NotFound()
    {
        var (context, _) = CreateHandler();

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            new GetPartnerByIdRequestHandler(new PartnerRepository(context))
                .HandleAsync(new GetPartnerByIdRequest { Id = Guid.NewGuid() }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }
}
