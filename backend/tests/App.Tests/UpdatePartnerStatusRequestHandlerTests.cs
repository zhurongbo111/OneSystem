using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Partners.UpdatePartnerStatus;
using App.Infrastructure;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// UpdatePartnerStatusRequestHandler 测试：停用 / 启用 / 不存在
/// </summary>
public class UpdatePartnerStatusRequestHandlerTests
{
    private static async Task<(AppDbContext Context, UpdatePartnerStatusRequestHandler Handler, StubCurrentUser User, Partner Partner)> CreateAsync()
    {
        var context = TestSupport.CreateDbContext();
        var user = new StubCurrentUser(Guid.NewGuid());
        var partner = new Partner
        {
            Id = Guid.NewGuid(),
            Name = "供应商X",
            Type = PartnerType.Supplier,
            Status = PartnerStatus.Enabled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        context.Partners.Add(partner);
        await context.SaveChangesAsync();
        var handler = new UpdatePartnerStatusRequestHandler(new PartnerRepository(context), user);
        return (context, handler, user, partner);
    }

    [Fact]
    public async Task 停用_应更新状态并记录审计()
    {
        var (context, handler, user, partner) = await CreateAsync();

        var result = await handler.HandleAsync(new UpdatePartnerStatusRequest
        {
            Id = partner.Id,
            Status = (int)PartnerStatus.Disabled,
        });

        Assert.Equal((int)PartnerStatus.Disabled, result.Status);
        var stored = await context.Partners.SingleAsync(p => p.Id == partner.Id);
        Assert.Equal(user.Id, stored.UpdatedBy.ToString());
    }

    [Fact]
    public async Task 启用_应恢复状态()
    {
        var (context, handler, _, partner) = await CreateAsync();
        partner.Status = PartnerStatus.Disabled;
        await context.SaveChangesAsync();

        var result = await handler.HandleAsync(new UpdatePartnerStatusRequest
        {
            Id = partner.Id,
            Status = (int)PartnerStatus.Enabled,
        });

        Assert.Equal((int)PartnerStatus.Enabled, result.Status);
    }

    [Fact]
    public async Task 停用_不存在_应报NotFound()
    {
        var (context, handler, _, _) = await CreateAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            handler.HandleAsync(new UpdatePartnerStatusRequest
            {
                Id = Guid.NewGuid(),
                Status = (int)PartnerStatus.Disabled,
            }));
        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }
}
