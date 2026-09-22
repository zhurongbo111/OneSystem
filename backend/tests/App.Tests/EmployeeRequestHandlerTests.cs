using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Employees.CreateEmployee;
using App.Core.Features.Employees.GetAvailableUsers;
using App.Core.Features.Employees.GetEmployeeById;
using App.Core.Features.Employees.GetEmployees;
using App.Core.Features.Employees.UpdateEmployee;
using App.Core.Features.Employees.UpdateEmployeeStatus;
using App.Infrastructure;
using App.Infrastructure.Persistence;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// 员工用例处理器测试（列表联查 / 新增唯一性与绑定冲突 / 编辑（工号不可改、绑定排除自身、状态与日期一致性）
/// / 在职离职切换 / 可选账号来源）
/// </summary>
public class EmployeeRequestHandlerTests
{
    private static Guid OperatorId { get; } = Guid.NewGuid();

    private static CreateEmployeeRequestHandler CreateCreateHandler(AppDbContext dbContext)
        => new(
            new EmployeeRepository(dbContext),
            new DepartmentRepository(dbContext),
            new PositionRepository(dbContext),
            new UserRepository(dbContext),
            new UnitOfWork(dbContext),
            new StubCurrentUser(OperatorId),
            TestSupport.AuditLogger);

    private static UpdateEmployeeRequestHandler CreateUpdateHandler(AppDbContext dbContext)
        => new(
            new EmployeeRepository(dbContext),
            new DepartmentRepository(dbContext),
            new PositionRepository(dbContext),
            new UserRepository(dbContext),
            new UnitOfWork(dbContext),
            new StubCurrentUser(OperatorId),
            TestSupport.AuditLogger);

    private static UpdateEmployeeStatusRequestHandler CreateStatusHandler(AppDbContext dbContext)
        => new(
            new EmployeeRepository(dbContext),
            new UnitOfWork(dbContext),
            new StubCurrentUser(OperatorId),
            TestSupport.AuditLogger);

    /// <summary>构建部门实体</summary>
    private static Department SeedDepartment(AppDbContext dbContext, string code = "D001", string name = "研发中心")
    {
        var now = DateTimeOffset.UtcNow;
        var department = new Department
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Status = DepartmentStatus.Enabled,
            CreatedAt = now,
            UpdatedAt = now,
        };
        dbContext.Departments.Add(department);
        dbContext.SaveChanges();
        return department;
    }

    /// <summary>构建岗位实体</summary>
    private static Position SeedPosition(AppDbContext dbContext, string code = "P001", string name = "后端工程师")
    {
        var now = DateTimeOffset.UtcNow;
        var position = new Position
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Status = PositionStatus.Enabled,
            CreatedAt = now,
            UpdatedAt = now,
        };
        dbContext.Positions.Add(position);
        dbContext.SaveChanges();
        return position;
    }

    /// <summary>构建员工实体</summary>
    private static Employee NewEmployee(
        string employeeNo = "E0001",
        string name = "张三",
        Guid? departmentId = null,
        Guid? positionId = null,
        Guid? userId = null,
        EmployeeStatus status = EmployeeStatus.Active,
        DateOnly? hireDate = null,
        DateOnly? resignDate = null,
        string? phone = null,
        string? email = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new Employee
        {
            Id = Guid.NewGuid(),
            EmployeeNo = employeeNo,
            Name = name,
            DepartmentId = departmentId,
            PositionId = positionId,
            UserId = userId,
            Status = status,
            HireDate = hireDate ?? new DateOnly(2026, 1, 1),
            ResignDate = resignDate,
            Phone = phone,
            Email = email,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static CreateEmployeeRequest NewCreateRequest(
        string employeeNo = "E0001",
        string name = "张三",
        Guid? departmentId = null,
        Guid? positionId = null,
        Guid? userId = null,
        string? phone = null,
        string? email = null)
        => new()
        {
            EmployeeNo = employeeNo,
            Name = name,
            DepartmentId = departmentId,
            PositionId = positionId,
            UserId = userId,
            Phone = phone,
            Email = email,
            HireDate = new DateOnly(2026, 1, 1),
            Status = (int)EmployeeStatus.Active,
        };

    // ============================== 新增 ==============================

    [Fact]
    public async Task CreateEmployee_合法请求_应落库并回读出部门岗位账号名称()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var department = SeedDepartment(dbContext);
        var position = SeedPosition(dbContext);
        var user = TestSupport.NewUser("zhangsan", "张三账号");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var result = await CreateCreateHandler(dbContext).HandleAsync(NewCreateRequest(
            departmentId: department.Id,
            positionId: position.Id,
            userId: user.Id,
            phone: "13800138000",
            email: "ZhangSan@Example.com"));

        var saved = await dbContext.Employees.SingleAsync();
        Assert.Equal("E0001", saved.EmployeeNo);
        Assert.Equal("研发中心", result.DepartmentName);
        Assert.Equal("后端工程师", result.PositionName);
        Assert.Equal("张三账号", result.UserDisplayName);
        // 邮箱统一小写存储（唯一性忽略大小写）
        Assert.Equal("zhangsan@example.com", saved.Email);
        Assert.Equal(OperatorId, saved.CreatedBy);
        Assert.Equal((int)EmployeeStatus.Active, result.Status);
        Assert.Equal("在职", result.StatusText);
    }

    [Fact]
    public async Task CreateEmployee_手机与邮箱为空串_应按未填写处理()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        await CreateCreateHandler(dbContext).HandleAsync(NewCreateRequest(phone: "   ", email: "  "));

        var saved = await dbContext.Employees.SingleAsync();
        Assert.Null(saved.Phone);
        Assert.Null(saved.Email);
    }

    [Fact]
    public async Task CreateEmployee_状态为离职且无离职日期_应补当天()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var request = new CreateEmployeeRequest
        {
            EmployeeNo = "E0002",
            Name = "李四",
            HireDate = new DateOnly(2026, 1, 1),
            Status = (int)EmployeeStatus.Resigned,
        };

        var result = await CreateCreateHandler(dbContext).HandleAsync(request);

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        Assert.Equal(today, (await dbContext.Employees.SingleAsync()).ResignDate);
        Assert.Equal("离职", result.StatusText);
    }

    [Fact]
    public async Task CreateEmployee_工号重复_应抛业务异常40145()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        dbContext.Employees.Add(NewEmployee("e0001"));
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(dbContext).HandleAsync(NewCreateRequest(employeeNo: "E0001")));

        Assert.Equal(ErrorCode.EmployeeNoExists, ex.Code);
        Assert.Equal(1, await dbContext.Employees.CountAsync());
    }

    [Fact]
    public async Task CreateEmployee_手机重复_应抛业务异常40147()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        dbContext.Employees.Add(NewEmployee("E0001", phone: "13800138000"));
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(dbContext).HandleAsync(NewCreateRequest(employeeNo: "E0002", phone: "13800138000")));

        Assert.Equal(ErrorCode.EmployeePhoneExists, ex.Code);
    }

    [Fact]
    public async Task CreateEmployee_邮箱重复_应抛业务异常40148()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        dbContext.Employees.Add(NewEmployee("E0001", email: "a@example.com"));
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(dbContext).HandleAsync(NewCreateRequest(employeeNo: "E0002", email: "A@Example.com")));

        Assert.Equal(ErrorCode.EmployeeEmailExists, ex.Code);
    }

    [Fact]
    public async Task CreateEmployee_账号已绑定其他员工_应抛业务异常40146()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var user = TestSupport.NewUser("zhangsan", "张三账号");
        dbContext.Users.Add(user);
        dbContext.Employees.Add(NewEmployee("E0001", userId: user.Id));
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(dbContext).HandleAsync(NewCreateRequest(employeeNo: "E0002", userId: user.Id)));

        Assert.Equal(ErrorCode.EmployeeUserBound, ex.Code);
    }

    [Fact]
    public async Task CreateEmployee_部门或岗位或账号不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var departmentEx = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(dbContext).HandleAsync(NewCreateRequest(departmentId: Guid.NewGuid())));
        Assert.Equal(ErrorCode.NotFound, departmentEx.Code);

        var positionEx = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(dbContext).HandleAsync(NewCreateRequest(positionId: Guid.NewGuid())));
        Assert.Equal(ErrorCode.NotFound, positionEx.Code);

        var userEx = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(dbContext).HandleAsync(NewCreateRequest(userId: Guid.NewGuid())));
        Assert.Equal(ErrorCode.NotFound, userEx.Code);

        Assert.Equal(0, await dbContext.Employees.CountAsync());
    }

    // ============================== 详情 / 列表 ==============================

    [Fact]
    public async Task GetEmployeeById_不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => new GetEmployeeByIdRequestHandler(new EmployeeRepository(dbContext))
                .HandleAsync(new GetEmployeeByIdRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task GetEmployees_筛选与联查_应返回部门岗位账号名称()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var department = SeedDepartment(dbContext);
        var position = SeedPosition(dbContext);
        var user = TestSupport.NewUser("zhangsan", "张三账号");
        dbContext.Users.Add(user);
        dbContext.Employees.Add(NewEmployee(
            "E0001",
            "张三",
            department.Id,
            position.Id,
            user.Id));
        dbContext.Employees.Add(NewEmployee("E0002", "李四", department.Id, status: EmployeeStatus.Resigned));
        await dbContext.SaveChangesAsync();

        var handler = new GetEmployeesRequestHandler(new EmployeeRepository(dbContext));

        var all = await handler.HandleAsync(new GetEmployeesRequest { Page = 1, PageSize = 20 });
        Assert.Equal(2, all.Total);

        var active = await handler.HandleAsync(new GetEmployeesRequest { Status = (int)EmployeeStatus.Active, Page = 1, PageSize = 20 });
        Assert.Equal(1, active.Total);
        var item = active.Items[0];
        Assert.Equal("研发中心", item.DepartmentName);
        Assert.Equal("后端工程师", item.PositionName);
        Assert.Equal("张三账号", item.UserDisplayName);
        Assert.Equal("在职", item.StatusText);

        var byKeyword = await handler.HandleAsync(new GetEmployeesRequest { Keyword = "李四", Page = 1, PageSize = 20 });
        Assert.Equal(1, byKeyword.Total);
        Assert.Equal("E0002", byKeyword.Items[0].EmployeeNo);

        var byDepartment = await handler.HandleAsync(new GetEmployeesRequest { DepartmentId = department.Id, Page = 1, PageSize = 20 });
        Assert.Equal(2, byDepartment.Total);

        var byPosition = await handler.HandleAsync(new GetEmployeesRequest { PositionId = position.Id, Page = 1, PageSize = 20 });
        Assert.Equal(1, byPosition.Total);
    }

    // ============================== 编辑 ==============================

    [Fact]
    public async Task UpdateEmployee_合法请求_应更新且工号保持不变()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var department = SeedDepartment(dbContext);
        var employee = NewEmployee(departmentId: department.Id, phone: "13800138000");
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();
        var originNo = employee.EmployeeNo;

        var result = await CreateUpdateHandler(dbContext).HandleAsync(new UpdateEmployeeRequest
        {
            Id = employee.Id,
            Name = "张三丰",
            Gender = (int)Gender.Male,
            Phone = "13800138000",
            Email = "new@example.com",
            DepartmentId = department.Id,
            HireDate = new DateOnly(2026, 2, 1),
            Status = (int)EmployeeStatus.Active,
            Remark = "备注",
        });

        var saved = await dbContext.Employees.SingleAsync();
        Assert.Equal(originNo, saved.EmployeeNo);
        Assert.Equal("张三丰", saved.Name);
        Assert.Equal(Gender.Male, saved.Gender);
        Assert.Equal(new DateOnly(2026, 2, 1), saved.HireDate);
        Assert.Equal("备注", saved.Remark);
        Assert.Equal(OperatorId, saved.UpdatedBy);
        Assert.Equal("张三丰", result.Name);
    }

    [Fact]
    public async Task UpdateEmployee_手机与他人重复_应抛业务异常40147_与自身相同应允许()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee("E0001", phone: "13800138000");
        dbContext.Employees.Add(employee);
        dbContext.Employees.Add(NewEmployee("E0002", "李四", phone: "13900139000"));
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdateEmployeeRequest
            {
                Id = employee.Id,
                Name = "张三",
                Phone = "13900139000",
                HireDate = new DateOnly(2026, 1, 1),
                Status = (int)EmployeeStatus.Active,
            }));

        Assert.Equal(ErrorCode.EmployeePhoneExists, ex.Code);
    }

    [Fact]
    public async Task UpdateEmployee_账号已绑定他人_应抛业务异常40146_绑定自身当前账号应允许()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var user = TestSupport.NewUser("zhangsan", "张三账号");
        var other = TestSupport.NewUser("lisi", "李四账号");
        dbContext.Users.AddRange(user, other);
        var employee = NewEmployee("E0001", userId: user.Id);
        dbContext.Employees.Add(employee);
        dbContext.Employees.Add(NewEmployee("E0002", "李四", userId: other.Id));
        await dbContext.SaveChangesAsync();

        // 保持自身已绑定的账号：排除自身后不冲突
        await CreateUpdateHandler(dbContext).HandleAsync(new UpdateEmployeeRequest
        {
            Id = employee.Id,
            Name = "张三",
            HireDate = new DateOnly(2026, 1, 1),
            Status = (int)EmployeeStatus.Active,
            UserId = user.Id,
        });

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdateEmployeeRequest
            {
                Id = employee.Id,
                Name = "张三",
                HireDate = new DateOnly(2026, 1, 1),
                Status = (int)EmployeeStatus.Active,
                UserId = other.Id,
            }));

        Assert.Equal(ErrorCode.EmployeeUserBound, ex.Code);
    }

    [Fact]
    public async Task UpdateEmployee_切离职应补日期_切回在职应清空日期()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        await CreateUpdateHandler(dbContext).HandleAsync(new UpdateEmployeeRequest
        {
            Id = employee.Id,
            Name = "张三",
            HireDate = new DateOnly(2026, 1, 1),
            Status = (int)EmployeeStatus.Resigned,
        });

        var resigned = await dbContext.Employees.AsNoTracking().SingleAsync(e => e.Id == employee.Id);
        Assert.Equal(EmployeeStatus.Resigned, resigned.Status);
        Assert.Equal(DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime), resigned.ResignDate);

        await CreateUpdateHandler(dbContext).HandleAsync(new UpdateEmployeeRequest
        {
            Id = employee.Id,
            Name = "张三",
            HireDate = new DateOnly(2026, 1, 1),
            Status = (int)EmployeeStatus.Active,
        });

        var reactivated = await dbContext.Employees.AsNoTracking().SingleAsync(e => e.Id == employee.Id);
        Assert.Equal(EmployeeStatus.Active, reactivated.Status);
        Assert.Null(reactivated.ResignDate);
    }

    [Fact]
    public async Task UpdateEmployee_不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdateEmployeeRequest
            {
                Id = Guid.NewGuid(),
                Name = "张三",
                HireDate = new DateOnly(2026, 1, 1),
                Status = (int)EmployeeStatus.Active,
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 在职 / 离职切换 ==============================

    [Fact]
    public async Task UpdateEmployeeStatus_置离职_应补离职日期且不删除记录()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        var result = await CreateStatusHandler(dbContext).HandleAsync(new UpdateEmployeeStatusRequest
        {
            Id = employee.Id,
            Status = (int)EmployeeStatus.Resigned,
        });

        var saved = await dbContext.Employees.SingleAsync();
        Assert.Equal(EmployeeStatus.Resigned, saved.Status);
        Assert.NotNull(saved.ResignDate);
        Assert.Equal("离职", result.StatusText);
    }

    [Fact]
    public async Task UpdateEmployeeStatus_复职_应清空离职日期()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee(status: EmployeeStatus.Resigned, resignDate: new DateOnly(2026, 3, 1));
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        await CreateStatusHandler(dbContext).HandleAsync(new UpdateEmployeeStatusRequest
        {
            Id = employee.Id,
            Status = (int)EmployeeStatus.Active,
        });

        var saved = await dbContext.Employees.SingleAsync();
        Assert.Equal(EmployeeStatus.Active, saved.Status);
        Assert.Null(saved.ResignDate);
    }

    [Fact]
    public async Task UpdateEmployeeStatus_不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateStatusHandler(dbContext).HandleAsync(new UpdateEmployeeStatusRequest
            {
                Id = Guid.NewGuid(),
                Status = (int)EmployeeStatus.Resigned,
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 可选账号来源 ==============================

    [Fact]
    public async Task GetAvailableUsers_应返回启用未绑定账号并放行当前员工已绑定账号()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var freeUser = TestSupport.NewUser("free", "空闲账号");
        var boundUser = TestSupport.NewUser("bound", "已绑定账号");
        var disabledUser = TestSupport.NewUser("disabled", "停用账号", status: UserStatus.Disabled);
        dbContext.Users.AddRange(freeUser, boundUser, disabledUser);
        var employee = NewEmployee("E0001", userId: boundUser.Id);
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        var handler = new GetAvailableUsersRequestHandler(new EmployeeRepository(dbContext));

        // 新增场景：仅启用且未被绑定的账号
        var forCreate = await handler.HandleAsync(new GetAvailableUsersRequest());
        Assert.Equal(["free"], forCreate.Select(u => u.Username).ToList());

        // 编辑场景：额外放行当前员工已绑定的账号
        var forEdit = await handler.HandleAsync(new GetAvailableUsersRequest { EmployeeId = employee.Id });
        Assert.Equal(["bound", "free"], forEdit.Select(u => u.Username).ToList());
    }
}
