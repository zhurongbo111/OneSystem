using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Positions.CreatePosition;
using App.Core.Features.Positions.DeletePosition;
using App.Core.Features.Positions.GetPositionById;
using App.Core.Features.Positions.GetPositionPicks;
using App.Core.Features.Positions.GetPositions;
using App.Core.Features.Positions.UpdatePosition;
using App.Core.Features.Positions.UpdatePositionStatus;
using App.Infrastructure;
using App.Infrastructure.Persistence;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// 岗位用例处理器测试（列表筛选分页 / 新增 / 详情 / 编辑唯一性 / 删除保护 / 启停 / 选择项）
/// </summary>
public class PositionRequestHandlerTests
{
    private static Guid OperatorId { get; } = Guid.NewGuid();

    private static GetPositionsRequestHandler CreateGetPositionsHandler(AppDbContext dbContext)
        => new(new PositionRepository(dbContext));

    private static CreatePositionRequestHandler CreateCreateHandler(AppDbContext dbContext)
        => new(
            new PositionRepository(dbContext),
            new UnitOfWork(dbContext),
            new StubCurrentUser(OperatorId),
            TestSupport.AuditLogger);

    private static UpdatePositionRequestHandler CreateUpdateHandler(AppDbContext dbContext)
        => new(
            new PositionRepository(dbContext),
            new UnitOfWork(dbContext),
            new StubCurrentUser(OperatorId),
            TestSupport.AuditLogger);

    private static DeletePositionRequestHandler CreateDeleteHandler(AppDbContext dbContext)
        => new(
            new PositionRepository(dbContext),
            new UnitOfWork(dbContext),
            TestSupport.AuditLogger);

    private static UpdatePositionStatusRequestHandler CreateStatusHandler(AppDbContext dbContext)
        => new(
            new PositionRepository(dbContext),
            new UnitOfWork(dbContext),
            new StubCurrentUser(OperatorId),
            TestSupport.AuditLogger);

    /// <summary>构建岗位实体（默认启用）</summary>
    private static Position NewPosition(
        string code = "P001",
        string name = "仓库管理员",
        PositionStatus status = PositionStatus.Enabled,
        string? remark = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new Position
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Status = status,
            Remark = remark,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>构建员工实体（挂到指定岗位）</summary>
    private static Employee NewEmployee(Guid? positionId)
    {
        var now = DateTimeOffset.UtcNow;
        return new Employee
        {
            Id = Guid.NewGuid(),
            EmployeeNo = $"E{Guid.NewGuid():N}"[..12],
            Name = "员工一",
            PositionId = positionId,
            HireDate = new DateOnly(2026, 1, 1),
            Status = EmployeeStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    // ============================== 列表 ==============================

    [Fact]
    public async Task GetPositions_关键词与状态筛选_应分页返回()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        dbContext.Positions.AddRange(
            NewPosition("P001", "仓库管理员"),
            NewPosition("P002", "采购专员"),
            NewPosition("P003", "仓管助理", PositionStatus.Disabled));
        await dbContext.SaveChangesAsync();

        var byKeyword = await CreateGetPositionsHandler(dbContext)
            .HandleAsync(new GetPositionsRequest { Keyword = "仓", Page = 1, PageSize = 20 });
        Assert.Equal(2, byKeyword.Total);

        var byStatus = await CreateGetPositionsHandler(dbContext)
            .HandleAsync(new GetPositionsRequest { Status = (int)PositionStatus.Disabled, Page = 1, PageSize = 20 });
        Assert.Equal(1, byStatus.Total);
        Assert.Equal("P003", byStatus.Items[0].Code);

        var paged = await CreateGetPositionsHandler(dbContext)
            .HandleAsync(new GetPositionsRequest { Page = 2, PageSize = 2 });
        Assert.Equal(3, paged.Total);
        Assert.Single(paged.Items);
        Assert.Equal(2, paged.Page);
    }

    // ============================== 新增 ==============================

    [Fact]
    public async Task CreatePosition_合法请求_应落库并写审计字段()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var result = await CreateCreateHandler(dbContext).HandleAsync(new CreatePositionRequest
        {
            Code = " P010 ",
            Name = " 出纳 ",
            Status = (int)PositionStatus.Enabled,
            Remark = "  资金收付  ",
        });

        var saved = await dbContext.Positions.SingleAsync();
        Assert.Equal("P010", saved.Code);
        Assert.Equal("出纳", saved.Name);
        Assert.Equal("资金收付", saved.Remark);
        Assert.Equal(OperatorId, saved.CreatedBy);
        Assert.Equal(saved.Id.ToString(), result.Id);
    }

    [Fact]
    public async Task CreatePosition_备注纯空白_应按清空处理()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        await CreateCreateHandler(dbContext).HandleAsync(new CreatePositionRequest
        {
            Code = "P010",
            Name = "出纳",
            Remark = "   ",
        });

        Assert.Null((await dbContext.Positions.SingleAsync()).Remark);
    }

    [Fact]
    public async Task CreatePosition_编码重复_应抛业务异常40142()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        dbContext.Positions.Add(NewPosition("p001"));
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(dbContext).HandleAsync(new CreatePositionRequest { Code = "P001", Name = "新岗位" }));

        Assert.Equal(ErrorCode.PositionCodeExists, ex.Code);
    }

    [Fact]
    public async Task CreatePosition_名称重复_应抛业务异常40143()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        dbContext.Positions.Add(NewPosition("P001", "仓库管理员"));
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(dbContext).HandleAsync(new CreatePositionRequest { Code = "P002", Name = "仓库管理员" }));

        Assert.Equal(ErrorCode.PositionNameExists, ex.Code);
    }

    // ============================== 详情 ==============================

    [Fact]
    public async Task GetPositionById_存在_应返回详情()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var position = NewPosition();
        dbContext.Positions.Add(position);
        await dbContext.SaveChangesAsync();

        var result = await new GetPositionByIdRequestHandler(new PositionRepository(dbContext))
            .HandleAsync(new GetPositionByIdRequest { Id = position.Id });

        Assert.Equal("P001", result.Code);
        Assert.Equal((int)PositionStatus.Enabled, result.Status);
    }

    [Fact]
    public async Task GetPositionById_不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => new GetPositionByIdRequestHandler(new PositionRepository(dbContext))
                .HandleAsync(new GetPositionByIdRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 编辑 ==============================

    [Fact]
    public async Task UpdatePosition_合法请求_应更新()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var position = NewPosition();
        dbContext.Positions.Add(position);
        await dbContext.SaveChangesAsync();

        var result = await CreateUpdateHandler(dbContext).HandleAsync(new UpdatePositionRequest
        {
            Id = position.Id,
            Code = "P001",
            Name = "仓库主管",
            Status = (int)PositionStatus.Disabled,
            Remark = "改了备注",
        });

        var saved = await dbContext.Positions.SingleAsync();
        Assert.Equal("仓库主管", saved.Name);
        Assert.Equal(PositionStatus.Disabled, saved.Status);
        Assert.Equal(OperatorId, saved.UpdatedBy);
        Assert.Equal("仓库主管", result.Name);
    }

    [Fact]
    public async Task UpdatePosition_名称改为自身_应允许_改为他岗位已有_应抛40143()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var position = NewPosition("P001", "仓库管理员");
        dbContext.Positions.Add(position);
        dbContext.Positions.Add(NewPosition("P002", "采购专员"));
        await dbContext.SaveChangesAsync();

        await CreateUpdateHandler(dbContext).HandleAsync(new UpdatePositionRequest
        {
            Id = position.Id,
            Code = "P001",
            Name = "仓库管理员",
            Status = (int)PositionStatus.Enabled,
        });

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdatePositionRequest
            {
                Id = position.Id,
                Code = "P003",
                Name = "采购专员",
                Status = (int)PositionStatus.Enabled,
            }));

        Assert.Equal(ErrorCode.PositionNameExists, ex.Code);
    }

    [Fact]
    public async Task UpdatePosition_编码与他岗位重复_应抛业务异常40142()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var position = NewPosition("P001");
        dbContext.Positions.Add(position);
        dbContext.Positions.Add(NewPosition("P002", "采购专员"));
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdatePositionRequest
            {
                Id = position.Id,
                Code = "P002",
                Name = "仓库管理员",
                Status = (int)PositionStatus.Enabled,
            }));

        Assert.Equal(ErrorCode.PositionCodeExists, ex.Code);
    }

    [Fact]
    public async Task UpdatePosition_不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdatePositionRequest
            {
                Id = Guid.NewGuid(),
                Code = "P001",
                Name = "仓库管理员",
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 删除保护 ==============================

    [Fact]
    public async Task DeletePosition_被员工引用_应抛业务异常40144()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var position = NewPosition();
        dbContext.Positions.Add(position);
        dbContext.Employees.Add(NewEmployee(position.Id));
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateDeleteHandler(dbContext).HandleAsync(new DeletePositionRequest { Id = position.Id }));

        Assert.Equal(ErrorCode.PositionInUse, ex.Code);
        Assert.Equal(1, await dbContext.Positions.CountAsync());
    }

    [Fact]
    public async Task DeletePosition_无引用_应删除()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var position = NewPosition();
        dbContext.Positions.Add(position);
        await dbContext.SaveChangesAsync();

        await CreateDeleteHandler(dbContext).HandleAsync(new DeletePositionRequest { Id = position.Id });

        Assert.Empty(await dbContext.Positions.ToListAsync());
    }

    [Fact]
    public async Task DeletePosition_不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateDeleteHandler(dbContext).HandleAsync(new DeletePositionRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 启停 ==============================

    [Fact]
    public async Task UpdatePositionStatus_停用_应更新状态()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var position = NewPosition();
        dbContext.Positions.Add(position);
        await dbContext.SaveChangesAsync();

        var result = await CreateStatusHandler(dbContext).HandleAsync(new UpdatePositionStatusRequest
        {
            Id = position.Id,
            Status = (int)PositionStatus.Disabled,
        });

        Assert.Equal((int)PositionStatus.Disabled, result.Status);
        Assert.Equal(PositionStatus.Disabled, (await dbContext.Positions.SingleAsync()).Status);
    }

    [Fact]
    public async Task UpdatePositionStatus_不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateStatusHandler(dbContext).HandleAsync(new UpdatePositionStatusRequest
            {
                Id = Guid.NewGuid(),
                Status = (int)PositionStatus.Enabled,
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 选择项 ==============================

    [Fact]
    public async Task GetPositionPicks_应只返回启用岗位()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        dbContext.Positions.AddRange(
            NewPosition("P002", "采购专员"),
            NewPosition("P001", "仓库管理员"),
            NewPosition("P003", "停用岗位", PositionStatus.Disabled));
        await dbContext.SaveChangesAsync();

        var picks = await new GetPositionPicksRequestHandler(new PositionRepository(dbContext))
            .HandleAsync(new GetPositionPicksRequest());

        Assert.Equal(["P001", "P002"], picks.Select(p => p.Code).ToList());
    }
}
