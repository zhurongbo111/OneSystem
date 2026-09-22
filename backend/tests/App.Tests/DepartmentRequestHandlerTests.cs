using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Departments.CreateDepartment;
using App.Core.Features.Departments.DeleteDepartment;
using App.Core.Features.Departments.GetDepartmentById;
using App.Core.Features.Departments.GetDepartments;
using App.Core.Features.Departments.UpdateDepartment;
using App.Core.Features.Departments.UpdateDepartmentStatus;
using App.Infrastructure;
using App.Infrastructure.Persistence;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// 部门用例处理器测试（树 / 新增 / 详情 / 编辑（防环）/ 删除保护 / 启停）
/// </summary>
public class DepartmentRequestHandlerTests
{
    private static Guid OperatorId { get; } = Guid.NewGuid();

    private static GetDepartmentsRequestHandler CreateGetDepartmentsHandler(AppDbContext dbContext)
        => new(new DepartmentRepository(dbContext));

    private static CreateDepartmentRequestHandler CreateCreateHandler(AppDbContext dbContext)
        => new(
            new DepartmentRepository(dbContext),
            new UnitOfWork(dbContext),
            new StubCurrentUser(OperatorId),
            TestSupport.AuditLogger);

    private static UpdateDepartmentRequestHandler CreateUpdateHandler(AppDbContext dbContext)
        => new(
            new DepartmentRepository(dbContext),
            new UnitOfWork(dbContext),
            new StubCurrentUser(OperatorId),
            TestSupport.AuditLogger);

    private static DeleteDepartmentRequestHandler CreateDeleteHandler(AppDbContext dbContext)
        => new(
            new DepartmentRepository(dbContext),
            new UnitOfWork(dbContext),
            TestSupport.AuditLogger);

    private static UpdateDepartmentStatusRequestHandler CreateStatusHandler(AppDbContext dbContext)
        => new(
            new DepartmentRepository(dbContext),
            new UnitOfWork(dbContext),
            new StubCurrentUser(OperatorId),
            TestSupport.AuditLogger);

    /// <summary>构建部门实体（默认启用）</summary>
    private static Department NewDepartment(
        string code = "D001",
        string name = "总部",
        Guid? parentId = null,
        int sortOrder = 0,
        DepartmentStatus status = DepartmentStatus.Enabled)
    {
        var now = DateTimeOffset.UtcNow;
        return new Department
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            ParentId = parentId,
            SortOrder = sortOrder,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>构建员工实体（挂到指定部门）</summary>
    private static Employee NewEmployee(Guid? departmentId, EmployeeStatus status = EmployeeStatus.Active)
    {
        var now = DateTimeOffset.UtcNow;
        return new Employee
        {
            Id = Guid.NewGuid(),
            EmployeeNo = $"E{Guid.NewGuid():N}"[..12],
            Name = "员工一",
            DepartmentId = departmentId,
            HireDate = new DateOnly(2026, 1, 1),
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    // ============================== 树 ==============================

    [Fact]
    public async Task GetDepartments_多级部门_应按上级组装树并统计在职人数()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var root = NewDepartment("D001", "总部", sortOrder: 2);
        var second = NewDepartment("D002", "研发中心", sortOrder: 1);
        var childA = NewDepartment("D003", "后端组", root.Id, sortOrder: 2);
        var childB = NewDepartment("D004", "前端组", root.Id, sortOrder: 1);
        var grandChild = NewDepartment("D005", "平台组", childA.Id);
        dbContext.Departments.AddRange(second, root, childB, childA, grandChild);
        // 在职 2 人 + 离职 1 人：树节点只计在职
        dbContext.Employees.AddRange(
            NewEmployee(childA.Id),
            NewEmployee(childA.Id),
            NewEmployee(childA.Id, EmployeeStatus.Resigned));
        await dbContext.SaveChangesAsync();

        var tree = await CreateGetDepartmentsHandler(dbContext).HandleAsync(new GetDepartmentsRequest());

        // 顶级节点按 SortOrder 升序：研发中心(1) → 总部(2)
        Assert.Equal(["研发中心", "总部"], tree.Select(n => n.Name).ToList());
        var rootNode = tree[1];
        Assert.Equal(["前端组", "后端组"], rootNode.Children.Select(n => n.Name).ToList());
        Assert.Equal(2, rootNode.Children[1].EmployeeCount);
        Assert.Equal("平台组", rootNode.Children[1].Children.Single().Name);
        Assert.Equal(0, rootNode.EmployeeCount);
    }

    // ============================== 新增 ==============================

    [Fact]
    public async Task CreateDepartment_合法请求_应落库并写审计字段()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var parent = NewDepartment();
        dbContext.Departments.Add(parent);
        await dbContext.SaveChangesAsync();

        var result = await CreateCreateHandler(dbContext).HandleAsync(new CreateDepartmentRequest
        {
            Code = " D010 ",
            Name = " 财务部 ",
            ParentId = parent.Id,
            SortOrder = 5,
            Status = (int)DepartmentStatus.Enabled,
            Remark = "  应收应付  ",
        });

        var saved = await dbContext.Departments.SingleAsync(d => d.Id != parent.Id);
        Assert.Equal("D010", saved.Code);
        Assert.Equal("财务部", saved.Name);
        Assert.Equal(parent.Id, saved.ParentId);
        Assert.Equal(5, saved.SortOrder);
        Assert.Equal("应收应付", saved.Remark);
        Assert.Equal(OperatorId, saved.CreatedBy);
        Assert.Equal(saved.Id.ToString(), result.Id);
        Assert.Equal((int)DepartmentStatus.Enabled, result.Status);
    }

    [Fact]
    public async Task CreateDepartment_编码重复_应抛业务异常40138()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        dbContext.Departments.Add(NewDepartment("d001", "总部"));
        await dbContext.SaveChangesAsync();

        // 大小写不敏感：d001 与 D001 视为重复
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(dbContext).HandleAsync(new CreateDepartmentRequest { Code = "D001", Name = "新部门" }));

        Assert.Equal(ErrorCode.DepartmentCodeExists, ex.Code);
        Assert.Equal(1, await dbContext.Departments.CountAsync());
    }

    [Fact]
    public async Task CreateDepartment_同一上级下重名_应抛业务异常40139()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var parent = NewDepartment();
        dbContext.Departments.Add(parent);
        dbContext.Departments.Add(NewDepartment("D002", "财务部", parent.Id));
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(dbContext).HandleAsync(new CreateDepartmentRequest { Code = "D003", Name = "财务部", ParentId = parent.Id }));

        Assert.Equal(ErrorCode.DepartmentNameExists, ex.Code);
    }

    [Fact]
    public async Task CreateDepartment_不同上级下同名_应允许()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var parentA = NewDepartment("D001", "华东区");
        var parentB = NewDepartment("D002", "华南区");
        dbContext.Departments.AddRange(parentA, parentB);
        dbContext.Departments.Add(NewDepartment("D003", "财务部", parentA.Id));
        await dbContext.SaveChangesAsync();

        var result = await CreateCreateHandler(dbContext).HandleAsync(new CreateDepartmentRequest
        {
            Code = "D004",
            Name = "财务部",
            ParentId = parentB.Id,
        });

        Assert.Equal("财务部", result.Name);
    }

    [Fact]
    public async Task CreateDepartment_上级不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(dbContext).HandleAsync(new CreateDepartmentRequest
            {
                Code = "D001",
                Name = "总部",
                ParentId = Guid.NewGuid(),
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
        Assert.Equal(0, await dbContext.Departments.CountAsync());
    }

    // ============================== 详情 ==============================

    [Fact]
    public async Task GetDepartmentById_不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => new GetDepartmentByIdRequestHandler(new DepartmentRepository(dbContext))
                .HandleAsync(new GetDepartmentByIdRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 编辑（防环）==============================

    [Fact]
    public async Task UpdateDepartment_改名与改上级_应更新并允许无变化的上游()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var root = NewDepartment("D001", "总部");
        var other = NewDepartment("D002", "分部");
        var child = NewDepartment("D003", "研发中心", root.Id);
        dbContext.Departments.AddRange(root, other, child);
        await dbContext.SaveChangesAsync();

        var result = await CreateUpdateHandler(dbContext).HandleAsync(new UpdateDepartmentRequest
        {
            Id = child.Id,
            Code = "D003",
            Name = "研发中心（改名）",
            ParentId = other.Id,
            SortOrder = 3,
            Status = (int)DepartmentStatus.Disabled,
            Remark = "备注",
        });

        var saved = await dbContext.Departments.SingleAsync(d => d.Id == child.Id);
        Assert.Equal("研发中心（改名）", saved.Name);
        Assert.Equal(other.Id, saved.ParentId);
        Assert.Equal(DepartmentStatus.Disabled, saved.Status);
        Assert.Equal(OperatorId, saved.UpdatedBy);
        Assert.Equal("备注", result.Remark);
    }

    [Fact]
    public async Task UpdateDepartment_上级设为自身_应抛业务异常40141()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var department = NewDepartment();
        dbContext.Departments.Add(department);
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdateDepartmentRequest
            {
                Id = department.Id,
                Code = department.Code,
                Name = department.Name,
                ParentId = department.Id,
            }));

        Assert.Equal(ErrorCode.DepartmentCycle, ex.Code);
    }

    [Fact]
    public async Task UpdateDepartment_上级设为自身后代_应抛业务异常40141()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var root = NewDepartment("D001", "总部");
        var child = NewDepartment("D002", "研发中心", root.Id);
        var grandChild = NewDepartment("D003", "后端组", child.Id);
        dbContext.Departments.AddRange(root, child, grandChild);
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdateDepartmentRequest
            {
                Id = root.Id,
                Code = root.Code,
                Name = root.Name,
                ParentId = grandChild.Id,
            }));

        Assert.Equal(ErrorCode.DepartmentCycle, ex.Code);
        Assert.Null((await dbContext.Departments.SingleAsync(d => d.Id == root.Id)).ParentId);
    }

    [Fact]
    public async Task UpdateDepartment_上级不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var department = NewDepartment();
        dbContext.Departments.Add(department);
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdateDepartmentRequest
            {
                Id = department.Id,
                Code = department.Code,
                Name = department.Name,
                ParentId = Guid.NewGuid(),
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task UpdateDepartment_与同级兄弟重名_应抛业务异常40139但更名自身允许()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var root = NewDepartment("D001", "总部");
        var childA = NewDepartment("D002", "财务部", root.Id);
        var childB = NewDepartment("D003", "人力资源部", root.Id);
        dbContext.Departments.AddRange(root, childA, childB);
        await dbContext.SaveChangesAsync();

        // 自身名称不变：重名检查排除自身，应放行
        await CreateUpdateHandler(dbContext).HandleAsync(new UpdateDepartmentRequest
        {
            Id = childA.Id,
            Code = childA.Code,
            Name = "财务部",
            ParentId = root.Id,
        });

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdateDepartmentRequest
            {
                Id = childB.Id,
                Code = childB.Code,
                Name = "财务部",
                ParentId = root.Id,
            }));

        Assert.Equal(ErrorCode.DepartmentNameExists, ex.Code);
    }

    [Fact]
    public async Task UpdateDepartment_不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdateDepartmentRequest
            {
                Id = Guid.NewGuid(),
                Code = "D001",
                Name = "总部",
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 删除保护 ==============================

    [Fact]
    public async Task DeleteDepartment_存在子部门_应抛业务异常40140()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var root = NewDepartment();
        dbContext.Departments.Add(root);
        dbContext.Departments.Add(NewDepartment("D002", "子部门", root.Id));
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateDeleteHandler(dbContext).HandleAsync(new DeleteDepartmentRequest { Id = root.Id }));

        Assert.Equal(ErrorCode.DepartmentInUse, ex.Code);
        Assert.Equal(2, await dbContext.Departments.CountAsync());
    }

    [Fact]
    public async Task DeleteDepartment_存在在职员工_应抛业务异常40140()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var department = NewDepartment();
        dbContext.Departments.Add(department);
        dbContext.Employees.Add(NewEmployee(department.Id));
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateDeleteHandler(dbContext).HandleAsync(new DeleteDepartmentRequest { Id = department.Id }));

        Assert.Equal(ErrorCode.DepartmentInUse, ex.Code);
        Assert.Equal(1, await dbContext.Departments.CountAsync());
    }

    [Fact]
    public async Task DeleteDepartment_仅离职员工引用_仍应抛业务异常40140()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var department = NewDepartment();
        dbContext.Departments.Add(department);
        // 离职员工仍保留部门引用（Restrict 外键），故同样禁止删除
        dbContext.Employees.Add(NewEmployee(department.Id, EmployeeStatus.Resigned));
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateDeleteHandler(dbContext).HandleAsync(new DeleteDepartmentRequest { Id = department.Id }));

        Assert.Equal(ErrorCode.DepartmentInUse, ex.Code);
        Assert.Equal(1, await dbContext.Departments.CountAsync());
    }

    [Fact]
    public async Task DeleteDepartment_空叶子部门_应删除()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var department = NewDepartment();
        dbContext.Departments.Add(department);
        await dbContext.SaveChangesAsync();

        await CreateDeleteHandler(dbContext).HandleAsync(new DeleteDepartmentRequest { Id = department.Id });

        Assert.Empty(await dbContext.Departments.ToListAsync());
    }

    [Fact]
    public async Task DeleteDepartment_不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateDeleteHandler(dbContext).HandleAsync(new DeleteDepartmentRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 启停 ==============================

    [Fact]
    public async Task UpdateDepartmentStatus_停用_应更新状态()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var department = NewDepartment();
        dbContext.Departments.Add(department);
        await dbContext.SaveChangesAsync();

        var result = await CreateStatusHandler(dbContext).HandleAsync(new UpdateDepartmentStatusRequest
        {
            Id = department.Id,
            Status = (int)DepartmentStatus.Disabled,
        });

        Assert.Equal((int)DepartmentStatus.Disabled, result.Status);
        Assert.Equal(DepartmentStatus.Disabled, (await dbContext.Departments.SingleAsync()).Status);
    }

    [Fact]
    public async Task UpdateDepartmentStatus_不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateStatusHandler(dbContext).HandleAsync(new UpdateDepartmentStatusRequest
            {
                Id = Guid.NewGuid(),
                Status = (int)DepartmentStatus.Enabled,
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }
}
