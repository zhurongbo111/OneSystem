using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Partners.UpdatePartner;
using App.Infrastructure;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// UpdatePartnerRequestHandler 测试：名称不可改、字段更新、审计、不存在
/// </summary>
public class UpdatePartnerRequestHandlerTests
{
    private static async Task<(AppDbContext Context, UpdatePartnerRequestHandler Handler, StubCurrentUser User, Partner Partner)> CreateAsync()
    {
        var context = TestSupport.CreateDbContext();
        var user = new StubCurrentUser(Guid.NewGuid());
        var partner = new Partner
        {
            Id = Guid.NewGuid(),
            Name = "原名称",
            Type = PartnerType.Supplier,
            Contact = "原联系人",
            Phone = "13800000000",
            Address = "原地址",
            Remark = "原备注",
            Status = PartnerStatus.Enabled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        context.Partners.Add(partner);
        await context.SaveChangesAsync();
        var handler = new UpdatePartnerRequestHandler(new PartnerRepository(context), user);
        return (context, handler, user, partner);
    }

    [Fact]
    public async Task 编辑_名称保持不变()
    {
        var (context, handler, _, partner) = await CreateAsync();

        var result = await handler.HandleAsync(new UpdatePartnerRequest
        {
            Id = partner.Id,
            Type = PartnerType.Both,
            Contact = "新联系人",
            Phone = "13900000000",
            Address = "新地址",
            Remark = "新备注",
        });

        Assert.Equal("原名称", result.Name);
        Assert.Equal((int)PartnerType.Both, result.Type);
        Assert.Equal("新联系人", result.Contact);
        var stored = await context.Partners.SingleAsync(p => p.Id == partner.Id);
        Assert.Equal("原名称", stored.Name);
    }

    [Fact]
    public async Task 编辑_空字符串字段应归一化为null()
    {
        var (context, handler, _, partner) = await CreateAsync();

        await handler.HandleAsync(new UpdatePartnerRequest
        {
            Id = partner.Id,
            Type = PartnerType.Supplier,
            Contact = "   ",
            Phone = null,
            Address = "",
            Remark = null,
        });

        var stored = await context.Partners.SingleAsync(p => p.Id == partner.Id);
        Assert.Null(stored.Contact);
        Assert.Null(stored.Phone);
        Assert.Null(stored.Address);
        Assert.Null(stored.Remark);
    }

    [Fact]
    public async Task 编辑_停用单位也应允许编辑()
    {
        var (context, handler, _, partner) = await CreateAsync();
        partner.Status = PartnerStatus.Disabled;
        await context.SaveChangesAsync();

        var result = await handler.HandleAsync(new UpdatePartnerRequest
        {
            Id = partner.Id,
            Type = PartnerType.Supplier,
            Contact = "停用后仍可编辑",
        });

        Assert.Equal("停用后仍可编辑", result.Contact);
        Assert.Equal((int)PartnerStatus.Disabled, result.Status);
    }

    [Fact]
    public async Task 编辑_审计字段应更新()
    {
        var (context, handler, user, partner) = await CreateAsync();

        await handler.HandleAsync(new UpdatePartnerRequest
        {
            Id = partner.Id,
            Type = PartnerType.Supplier,
        });

        var stored = await context.Partners.SingleAsync(p => p.Id == partner.Id);
        Assert.Equal(user.Id, stored.UpdatedBy.ToString());
        Assert.Null(stored.CreatedBy); // 未记录创建人，仅验证更新人
    }

    [Fact]
    public async Task 编辑_不存在_应报NotFound()
    {
        var (context, handler, _, _) = await CreateAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            handler.HandleAsync(new UpdatePartnerRequest
            {
                Id = Guid.NewGuid(),
                Type = PartnerType.Supplier,
            }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }
}
