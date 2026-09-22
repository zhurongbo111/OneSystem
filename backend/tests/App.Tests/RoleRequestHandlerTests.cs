using App.Core;
using App.Core.Auth;
using App.Core.Errors;
using App.Core.Features.Roles.CreateRole;
using App.Core.Features.Roles.DeleteRole;
using App.Core.Features.Roles.GetRoleById;
using App.Core.Features.Roles.GetRoles;
using App.Core.Features.Roles.UpdateRole;
using App.Infrastructure;
using App.Infrastructure.Persistence;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// 角色用例处理器测试（列表 / 新增 / 详情 / 编辑 / 删除）
/// </summary>
public class RoleRequestHandlerTests
{
    private static Guid OperatorId { get; } = Guid.NewGuid();

    private static GetRolesRequestHandler CreateGetRolesHandler(AppDbContext dbContext)
        => new(new RoleRepository(dbContext));

    private static CreateRoleRequestHandler CreateCreateRoleHandler(AppDbContext dbContext)
        => new(
            new RoleRepository(dbContext),
            new UnitOfWork(dbContext),
            new StubCurrentUser(OperatorId));

    private static UpdateRoleRequestHandler CreateUpdateRoleHandler(AppDbContext dbContext)
        => new(
            new RoleRepository(dbContext),
            new UserRoleRepository(dbContext),
            new UnitOfWork(dbContext),
            new StubCurrentUser(OperatorId));

    private static DeleteRoleRequestHandler CreateDeleteRoleHandler(AppDbContext dbContext)
        => new(
            new RoleRepository(dbContext),
            new UserRoleRepository(dbContext),
            new UnitOfWork(dbContext));

    private static CreateRoleRequest NewCreateRoleRequest(
        string name = "仓库主管",
        string? remark = "负责仓库作业",
        IReadOnlyList<string>? permissionKeys = null)
        => new()
        {
            Name = name,
            Remark = remark,
            PermissionKeys = permissionKeys ?? [Permissions.PurchasesView, Permissions.PurchasesCreate],
        };

    [Fact]
    public async Task GetRoles_无筛选_应分页返回并记录权限数与用户数()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var role = TestSupport.SeedRole(dbContext, "仓库主管", false, Permissions.PurchasesView, Permissions.PurchasesCreate);
        var user = TestSupport.NewUser("alice", "张三");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        TestSupport.BindRole(dbContext, user, role);

        var result = await CreateGetRolesHandler(dbContext).HandleAsync(new GetRolesRequest());

        Assert.Equal(1, result.Total);
        var item = result.Items[0];
        Assert.Equal("仓库主管", item.Name);
        Assert.False(item.IsBuiltin);
        Assert.Equal(2, item.PermissionCount);
        Assert.Equal(1, item.UserCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public async Task GetRoles_关键词_应同时匹配名称与备注()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        TestSupport.SeedRole(dbContext, "仓库主管", false, Permissions.PurchasesView);
        TestSupport.SeedRole(dbContext, "采购专员", false, Permissions.PurchasesView);
        dbContext.SaveChanges();

        var byName = await CreateGetRolesHandler(dbContext).HandleAsync(new GetRolesRequest { Keyword = "仓库" });
        Assert.Equal(1, byName.Total);
        Assert.Equal("仓库主管", byName.Items[0].Name);

        var byRemark = await CreateGetRolesHandler(dbContext).HandleAsync(new GetRolesRequest { Keyword = "采购" });
        Assert.Equal(1, byRemark.Total);
        Assert.Equal("采购专员", byRemark.Items[0].Name);
    }

    [Fact]
    public async Task CreateRole_合法请求_应落库角色与权限点并写审计字段()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var result = await CreateCreateRoleHandler(dbContext).HandleAsync(NewCreateRoleRequest());

        var saved = await dbContext.Roles.SingleAsync();
        Assert.Equal("仓库主管", saved.Name);
        Assert.Equal("负责仓库作业", saved.Remark);
        Assert.False(saved.IsBuiltin);
        Assert.Equal(OperatorId, saved.CreatedBy);
        Assert.Equal(2, await dbContext.RolePermissions.CountAsync());
        Assert.Equal(2, result.PermissionKeys.Count);
        Assert.Equal(0, result.UserCount);
    }

    [Fact]
    public async Task CreateRole_备注为纯空白_应按清空处理()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        await CreateCreateRoleHandler(dbContext).HandleAsync(NewCreateRoleRequest(remark: "   "));

        Assert.Null((await dbContext.Roles.SingleAsync()).Remark);
    }

    [Fact]
    public async Task CreateRole_名称重复_应抛业务异常40173()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        TestSupport.SeedRole(dbContext, "采购专员");

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateRoleHandler(dbContext).HandleAsync(NewCreateRoleRequest(name: "采购专员")));

        Assert.Equal(ErrorCode.RoleNameExists, ex.Code);
        Assert.Equal(1, await dbContext.Roles.CountAsync());
    }

    [Fact]
    public async Task CreateRole_名称大小写不同_应判为重名()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        TestSupport.SeedRole(dbContext, "operator");

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateRoleHandler(dbContext).HandleAsync(NewCreateRoleRequest(name: "OPERATOR")));

        Assert.Equal(ErrorCode.RoleNameExists, ex.Code);
    }

    [Fact]
    public async Task CreateRole_权限点非法_应抛业务异常40000且不落角色()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateRoleHandler(dbContext).HandleAsync(NewCreateRoleRequest(permissionKeys: ["products.fly"])));

        Assert.Equal(ErrorCode.Validation, ex.Code);
        Assert.Empty(await dbContext.Roles.ToListAsync());
        Assert.Empty(await dbContext.RolePermissions.ToListAsync());
    }

    [Fact]
    public async Task CreateRole_权限点重复_应去重后落库()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var result = await CreateCreateRoleHandler(dbContext).HandleAsync(
            NewCreateRoleRequest(permissionKeys: [Permissions.PurchasesView, Permissions.PurchasesView]));

        Assert.Single(result.PermissionKeys);
        Assert.Equal(1, await dbContext.RolePermissions.CountAsync());
    }

    [Fact]
    public async Task GetRoleById_角色存在_应返回详情与绑定用户数()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var role = TestSupport.SeedRole(dbContext, "仓库主管", false, Permissions.PurchasesView);
        var user = TestSupport.NewUser("alice", "张三");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        TestSupport.BindRole(dbContext, user, role);

        var result = await new GetRoleByIdRequestHandler(new RoleRepository(dbContext), new UserRoleRepository(dbContext))
            .HandleAsync(new GetRoleByIdRequest { Id = role.Id });

        Assert.Equal("仓库主管", result.Name);
        Assert.Equal([Permissions.PurchasesView], result.PermissionKeys);
        Assert.Equal(1, result.UserCount);
    }

    [Fact]
    public async Task GetRoleById_角色不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => new GetRoleByIdRequestHandler(new RoleRepository(dbContext), new UserRoleRepository(dbContext))
                .HandleAsync(new GetRoleByIdRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task UpdateRole_全量替换_应更新名称备注与权限点()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var role = TestSupport.SeedRole(
            dbContext,
            "仓库主管",
            false,
            Permissions.PurchasesView,
            Permissions.PurchasesCreate,
            Permissions.PurchasesVoid);

        var result = await CreateUpdateRoleHandler(dbContext).HandleAsync(new UpdateRoleRequest
        {
            Id = role.Id,
            Name = "仓库专员",
            Remark = "改了备注",
            PermissionKeys = [Permissions.PurchasesView],
        });

        var saved = await dbContext.Roles.SingleAsync();
        Assert.Equal("仓库专员", saved.Name);
        Assert.Equal("改了备注", saved.Remark);
        Assert.Equal(OperatorId, saved.UpdatedBy);
        Assert.Equal([Permissions.PurchasesView], await dbContext.RolePermissions.Select(rp => rp.PermissionKey).ToListAsync());
        Assert.Single(result.PermissionKeys);
    }

    [Fact]
    public async Task UpdateRole_备注为空_应清空备注()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var role = TestSupport.SeedRole(dbContext, "仓库主管", false, Permissions.PurchasesView);

        await CreateUpdateRoleHandler(dbContext).HandleAsync(new UpdateRoleRequest
        {
            Id = role.Id,
            Name = "仓库主管",
            Remark = "   ",
            PermissionKeys = [Permissions.PurchasesView],
        });

        Assert.Null((await dbContext.Roles.SingleAsync()).Remark);
    }

    [Fact]
    public async Task UpdateRole_名称改为自身_应允许()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var role = TestSupport.SeedRole(dbContext, "仓库主管", false, Permissions.PurchasesView);

        var result = await CreateUpdateRoleHandler(dbContext).HandleAsync(new UpdateRoleRequest
        {
            Id = role.Id,
            Name = "仓库主管",
            PermissionKeys = [Permissions.PurchasesView],
        });

        Assert.Equal("仓库主管", result.Name);
    }

    [Fact]
    public async Task UpdateRole_名称改为他角色已有_应抛业务异常40173()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var role = TestSupport.SeedRole(dbContext, "仓库主管", false, Permissions.PurchasesView);
        TestSupport.SeedRole(dbContext, "采购专员");

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateRoleHandler(dbContext).HandleAsync(new UpdateRoleRequest
            {
                Id = role.Id,
                Name = "采购专员",
                PermissionKeys = [Permissions.PurchasesView],
            }));

        Assert.Equal(ErrorCode.RoleNameExists, ex.Code);
        Assert.Equal("仓库主管", (await dbContext.Roles.SingleAsync(r => r.Id == role.Id)).Name);
    }

    [Fact]
    public async Task UpdateRole_内置超级管理员_应抛业务异常40175()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var role = TestSupport.SeedRole(dbContext, BuiltinRoles.SuperAdmin, true);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateRoleHandler(dbContext).HandleAsync(new UpdateRoleRequest
            {
                Id = role.Id,
                Name = "改名试试",
                PermissionKeys = [Permissions.PurchasesView],
            }));

        Assert.Equal(ErrorCode.RoleBuiltinImmutable, ex.Code);
        Assert.Equal(BuiltinRoles.SuperAdmin, (await dbContext.Roles.SingleAsync()).Name);
    }

    [Fact]
    public async Task UpdateRole_角色不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateRoleHandler(dbContext).HandleAsync(new UpdateRoleRequest
            {
                Id = Guid.NewGuid(),
                Name = "任意角色",
                PermissionKeys = [Permissions.PurchasesView],
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task DeleteRole_无用户绑定_应删除角色及其权限行()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var role = TestSupport.SeedRole(dbContext, "仓库主管", false, Permissions.PurchasesView, Permissions.PurchasesCreate);

        await CreateDeleteRoleHandler(dbContext).HandleAsync(new DeleteRoleRequest { Id = role.Id });

        Assert.Empty(await dbContext.Roles.ToListAsync());
        Assert.Empty(await dbContext.RolePermissions.ToListAsync());
    }

    [Fact]
    public async Task DeleteRole_内置角色_应抛业务异常40175()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var role = TestSupport.SeedRole(dbContext, BuiltinRoles.Staff, true);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateDeleteRoleHandler(dbContext).HandleAsync(new DeleteRoleRequest { Id = role.Id }));

        Assert.Equal(ErrorCode.RoleBuiltinImmutable, ex.Code);
        Assert.Equal(1, await dbContext.Roles.CountAsync());
    }

    [Fact]
    public async Task DeleteRole_有用户绑定_应抛业务异常40174()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var role = TestSupport.SeedRole(dbContext, "仓库主管");
        var user = TestSupport.NewUser("alice", "张三");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        TestSupport.BindRole(dbContext, user, role);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateDeleteRoleHandler(dbContext).HandleAsync(new DeleteRoleRequest { Id = role.Id }));

        Assert.Equal(ErrorCode.RoleInUse, ex.Code);
        Assert.Equal(1, await dbContext.Roles.CountAsync());
    }

    [Fact]
    public async Task DeleteRole_角色不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateDeleteRoleHandler(dbContext).HandleAsync(new DeleteRoleRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }
}
