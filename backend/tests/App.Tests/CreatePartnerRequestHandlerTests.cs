using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Partners.CreatePartner;
using App.Infrastructure;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// CreatePartnerRequestHandler 测试：名称唯一（大小写不敏感）、默认启用、审计字段
/// </summary>
public class CreatePartnerRequestHandlerTests
{
    private static (AppDbContext Context, CreatePartnerRequestHandler Handler, StubCurrentUser User) CreateHandler()
    {
        var context = TestSupport.CreateDbContext();
        var user = new StubCurrentUser(Guid.NewGuid());
        var handler = new CreatePartnerRequestHandler(new PartnerRepository(context), user);
        return (context, handler, user);
    }

    [Fact]
    public async Task 新增往来单位_应成功且默认启用()
    {
        var (context, handler, _) = CreateHandler();

        var result = await handler.HandleAsync(new CreatePartnerRequest
        {
            Name = "杭州供应商A",
            Type = PartnerType.Supplier,
            Contact = "张三",
            Phone = "13800000000",
            Address = "杭州市西湖区",
        });

        Assert.Equal("杭州供应商A", result.Name);
        Assert.Equal((int)PartnerType.Supplier, result.Type);
        Assert.Equal((int)PartnerStatus.Enabled, result.Status);
        Assert.NotNull(await context.Partners.SingleAsync(p => p.Id.ToString() == result.Id));
    }

    [Fact]
    public async Task 新增往来单位_名称重复_应报PartnerNameExists()
    {
        var (_, handler, _) = CreateHandler();

        await handler.HandleAsync(new CreatePartnerRequest { Name = "供应商B", Type = PartnerType.Supplier });

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            handler.HandleAsync(new CreatePartnerRequest { Name = "供应商B", Type = PartnerType.Customer }));
        Assert.Equal(ErrorCode.PartnerNameExists, ex.Code);
    }

    [Fact]
    public async Task 新增往来单位_名称大小写不同_视为重复()
    {
        var (_, handler, _) = CreateHandler();

        await handler.HandleAsync(new CreatePartnerRequest { Name = "ABC公司", Type = PartnerType.Both });

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            handler.HandleAsync(new CreatePartnerRequest { Name = "abc公司", Type = PartnerType.Supplier }));
        Assert.Equal(ErrorCode.PartnerNameExists, ex.Code);
    }

    [Fact]
    public async Task 新增往来单位_空白字段应归一化为null()
    {
        var (context, handler, _) = CreateHandler();

        await handler.HandleAsync(new CreatePartnerRequest
        {
            Name = "  供应商C  ",
            Type = PartnerType.Supplier,
            Contact = "   ",
            Phone = "  13900000000  ",
        });

        var partner = await context.Partners.SingleAsync(p => p.Name == "供应商C");
        Assert.Null(partner.Contact);
        Assert.Equal("13900000000", partner.Phone);
    }

    [Fact]
    public async Task 新增往来单位_审计字段应记录当前用户()
    {
        var (context, handler, user) = CreateHandler();

        await handler.HandleAsync(new CreatePartnerRequest { Name = "供应商D", Type = PartnerType.Supplier });

        var partner = await context.Partners.SingleAsync(p => p.Name == "供应商D");
        Assert.Equal(user.Id, partner.CreatedBy.ToString());
        Assert.Equal(user.Id, partner.UpdatedBy.ToString());
    }
}
