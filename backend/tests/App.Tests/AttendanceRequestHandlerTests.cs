using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Attendances.CreateAttendance;
using App.Core.Features.Attendances.DeleteAttendance;
using App.Core.Features.Attendances.GetAttendances;
using App.Core.Features.Attendances.UpdateAttendance;
using App.Infrastructure;
using App.Infrastructure.Persistence;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// 考勤登记用例处理器测试（044-erp-hcm-payroll 任务 5.1）：
/// 新增（在职校验 / 区间重叠）、编辑（排除自身 / 快照刷新）、删除、列表筛选分页。
/// </summary>
public class AttendanceRequestHandlerTests
{
    private static Guid OperatorId { get; } = Guid.NewGuid();

    private static GetAttendancesRequestHandler CreateGetHandler(AppDbContext dbContext)
        => new(new AttendanceRepository(dbContext));

    private static CreateAttendanceRequestHandler CreateCreateHandler(AppDbContext dbContext)
        => new(
            new AttendanceRepository(dbContext),
            new EmployeeRepository(dbContext),
            new UnitOfWork(dbContext),
            new StubCurrentUser(OperatorId),
            TestSupport.AuditLogger);

    private static UpdateAttendanceRequestHandler CreateUpdateHandler(AppDbContext dbContext)
        => new(
            new AttendanceRepository(dbContext),
            new EmployeeRepository(dbContext),
            new UnitOfWork(dbContext),
            new StubCurrentUser(OperatorId),
            TestSupport.AuditLogger);

    private static DeleteAttendanceRequestHandler CreateDeleteHandler(AppDbContext dbContext)
        => new(
            new AttendanceRepository(dbContext),
            new UnitOfWork(dbContext),
            TestSupport.AuditLogger);

    /// <summary>构建员工实体（默认在职）</summary>
    private static Employee NewEmployee(string name = "张三", EmployeeStatus status = EmployeeStatus.Active)
    {
        var now = DateTimeOffset.UtcNow;
        return new Employee
        {
            Id = Guid.NewGuid(),
            EmployeeNo = $"E{Guid.NewGuid():N}"[..12],
            Name = name,
            HireDate = new DateOnly(2026, 1, 1),
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    // ============================== 新增 ==============================

    [Fact]
    public async Task CreateAttendance_合法请求_应落库并写姓名快照与审计字段()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        var result = await CreateCreateHandler(dbContext).HandleAsync(new CreateAttendanceRequest
        {
            EmployeeId = employee.Id,
            Type = (int)AttendanceType.Leave,
            StartDate = new DateOnly(2026, 9, 21),
            EndDate = new DateOnly(2026, 9, 22),
            Remark = " 家中有事 ",
        });

        var saved = await dbContext.Attendances.SingleAsync();
        Assert.Equal(employee.Id, saved.EmployeeId);
        Assert.Equal(employee.Name, saved.EmployeeName);
        Assert.Equal(AttendanceType.Leave, saved.Type);
        Assert.Equal(new DateOnly(2026, 9, 21), saved.StartDate);
        Assert.Equal(new DateOnly(2026, 9, 22), saved.EndDate);
        Assert.Equal("家中有事", saved.Remark);
        Assert.Equal(OperatorId, saved.CreatedBy);
        // 派生字段：天数含首尾 = 2 天
        Assert.Equal(2, result.Days);
        Assert.Equal("请假", result.TypeText);
    }

    [Fact]
    public async Task CreateAttendance_同日单天_天数应为1()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        var result = await CreateCreateHandler(dbContext).HandleAsync(new CreateAttendanceRequest
        {
            EmployeeId = employee.Id,
            Type = (int)AttendanceType.Overtime,
            StartDate = new DateOnly(2026, 9, 21),
            EndDate = new DateOnly(2026, 9, 21),
        });

        Assert.Equal(1, result.Days);
        Assert.Equal("加班", result.TypeText);
    }

    [Fact]
    public async Task CreateAttendance_员工不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(dbContext).HandleAsync(new CreateAttendanceRequest
            {
                EmployeeId = Guid.NewGuid(),
                Type = (int)AttendanceType.Leave,
                StartDate = new DateOnly(2026, 9, 21),
                EndDate = new DateOnly(2026, 9, 22),
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
        Assert.Equal(0, await dbContext.Attendances.CountAsync());
    }

    [Fact]
    public async Task CreateAttendance_离职员工_应抛业务异常40000()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee(status: EmployeeStatus.Resigned);
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(dbContext).HandleAsync(new CreateAttendanceRequest
            {
                EmployeeId = employee.Id,
                Type = (int)AttendanceType.Leave,
                StartDate = new DateOnly(2026, 9, 21),
                EndDate = new DateOnly(2026, 9, 22),
            }));

        Assert.Equal(ErrorCode.Validation, ex.Code);
        Assert.Equal(0, await dbContext.Attendances.CountAsync());
    }

    [Fact]
    public async Task CreateAttendance_同员工同类区间重叠_应抛业务异常40000且提示冲突区间()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        dbContext.Attendances.Add(new Attendance
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            EmployeeName = employee.Name,
            Type = AttendanceType.Leave,
            StartDate = new DateOnly(2026, 9, 21),
            EndDate = new DateOnly(2026, 9, 23),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        // 与既有区间部分重叠（09-22 落在 09-21 ~ 09-23 内）
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(dbContext).HandleAsync(new CreateAttendanceRequest
            {
                EmployeeId = employee.Id,
                Type = (int)AttendanceType.Leave,
                StartDate = new DateOnly(2026, 9, 22),
                EndDate = new DateOnly(2026, 9, 24),
            }));

        Assert.Equal(ErrorCode.Validation, ex.Code);
        Assert.Contains("2026-09-21", ex.Message, StringComparison.Ordinal);
        Assert.Contains("2026-09-23", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, await dbContext.Attendances.CountAsync());
    }

    [Fact]
    public async Task CreateAttendance_相邻但不重叠的区间_应允许()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        dbContext.Attendances.Add(new Attendance
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            EmployeeName = employee.Name,
            Type = AttendanceType.Leave,
            StartDate = new DateOnly(2026, 9, 21),
            EndDate = new DateOnly(2026, 9, 22),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        // 紧接次日开始（09-23）不构成重叠
        await CreateCreateHandler(dbContext).HandleAsync(new CreateAttendanceRequest
        {
            EmployeeId = employee.Id,
            Type = (int)AttendanceType.Leave,
            StartDate = new DateOnly(2026, 9, 23),
            EndDate = new DateOnly(2026, 9, 24),
        });

        Assert.Equal(2, await dbContext.Attendances.CountAsync());
    }

    [Fact]
    public async Task CreateAttendance_不同类型同日_应允许()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        dbContext.Attendances.Add(new Attendance
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            EmployeeName = employee.Name,
            Type = AttendanceType.Leave,
            StartDate = new DateOnly(2026, 9, 21),
            EndDate = new DateOnly(2026, 9, 21),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        await CreateCreateHandler(dbContext).HandleAsync(new CreateAttendanceRequest
        {
            EmployeeId = employee.Id,
            Type = (int)AttendanceType.Overtime,
            StartDate = new DateOnly(2026, 9, 21),
            EndDate = new DateOnly(2026, 9, 21),
        });

        Assert.Equal(2, await dbContext.Attendances.CountAsync());
    }

    // ============================== 编辑 ==============================

    [Fact]
    public async Task UpdateAttendance_改员工与日期_应更新并刷新姓名快照()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee("张三");
        var other = NewEmployee("李四");
        dbContext.Employees.AddRange(employee, other);
        var attendance = new Attendance
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            EmployeeName = employee.Name,
            Type = AttendanceType.Leave,
            StartDate = new DateOnly(2026, 9, 21),
            EndDate = new DateOnly(2026, 9, 22),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        dbContext.Attendances.Add(attendance);
        await dbContext.SaveChangesAsync();

        var result = await CreateUpdateHandler(dbContext).HandleAsync(new UpdateAttendanceRequest
        {
            Id = attendance.Id,
            EmployeeId = other.Id,
            Type = (int)AttendanceType.Overtime,
            StartDate = new DateOnly(2026, 9, 25),
            EndDate = new DateOnly(2026, 9, 26),
            Remark = "项目赶工",
        });

        var saved = await dbContext.Attendances.SingleAsync();
        Assert.Equal(other.Id, saved.EmployeeId);
        Assert.Equal("李四", saved.EmployeeName);
        Assert.Equal(AttendanceType.Overtime, saved.Type);
        Assert.Equal(new DateOnly(2026, 9, 25), saved.StartDate);
        Assert.Equal("项目赶工", saved.Remark);
        Assert.Equal(OperatorId, saved.UpdatedBy);
        Assert.Equal("李四", result.EmployeeName);
    }

    [Fact]
    public async Task UpdateAttendance_保持原值_不应与自身判为重叠()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        var attendance = new Attendance
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            EmployeeName = employee.Name,
            Type = AttendanceType.Leave,
            StartDate = new DateOnly(2026, 9, 21),
            EndDate = new DateOnly(2026, 9, 22),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        dbContext.Attendances.Add(attendance);
        await dbContext.SaveChangesAsync();

        await CreateUpdateHandler(dbContext).HandleAsync(new UpdateAttendanceRequest
        {
            Id = attendance.Id,
            EmployeeId = employee.Id,
            Type = (int)AttendanceType.Leave,
            StartDate = new DateOnly(2026, 9, 21),
            EndDate = new DateOnly(2026, 9, 22),
        });

        Assert.Equal(new DateOnly(2026, 9, 22), (await dbContext.Attendances.SingleAsync()).EndDate);
    }

    [Fact]
    public async Task UpdateAttendance_与另一条重叠_应抛业务异常40000()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        var first = new Attendance
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            EmployeeName = employee.Name,
            Type = AttendanceType.Leave,
            StartDate = new DateOnly(2026, 9, 21),
            EndDate = new DateOnly(2026, 9, 22),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        var second = new Attendance
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            EmployeeName = employee.Name,
            Type = AttendanceType.Leave,
            StartDate = new DateOnly(2026, 9, 25),
            EndDate = new DateOnly(2026, 9, 26),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        dbContext.Attendances.AddRange(first, second);
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdateAttendanceRequest
            {
                Id = second.Id,
                EmployeeId = employee.Id,
                Type = (int)AttendanceType.Leave,
                StartDate = new DateOnly(2026, 9, 22),
                EndDate = new DateOnly(2026, 9, 23),
            }));

        Assert.Equal(ErrorCode.Validation, ex.Code);
    }

    [Fact]
    public async Task UpdateAttendance_记录不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdateAttendanceRequest
            {
                Id = Guid.NewGuid(),
                EmployeeId = employee.Id,
                Type = (int)AttendanceType.Leave,
                StartDate = new DateOnly(2026, 9, 21),
                EndDate = new DateOnly(2026, 9, 22),
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task UpdateAttendance_员工离职_应抛业务异常40000()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        var attendance = new Attendance
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            EmployeeName = employee.Name,
            Type = AttendanceType.Leave,
            StartDate = new DateOnly(2026, 9, 21),
            EndDate = new DateOnly(2026, 9, 22),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        dbContext.Attendances.Add(attendance);
        await dbContext.SaveChangesAsync();

        // 员工随后离职
        employee.Status = EmployeeStatus.Resigned;
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdateAttendanceRequest
            {
                Id = attendance.Id,
                EmployeeId = employee.Id,
                Type = (int)AttendanceType.Leave,
                StartDate = new DateOnly(2026, 9, 21),
                EndDate = new DateOnly(2026, 9, 22),
            }));

        Assert.Equal(ErrorCode.Validation, ex.Code);
    }

    // ============================== 删除 ==============================

    [Fact]
    public async Task DeleteAttendance_存在_应物理删除()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        var attendance = new Attendance
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            EmployeeName = employee.Name,
            Type = AttendanceType.Leave,
            StartDate = new DateOnly(2026, 9, 21),
            EndDate = new DateOnly(2026, 9, 22),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        dbContext.Attendances.Add(attendance);
        await dbContext.SaveChangesAsync();

        await CreateDeleteHandler(dbContext).HandleAsync(new DeleteAttendanceRequest { Id = attendance.Id });

        Assert.Empty(await dbContext.Attendances.ToListAsync());
    }

    [Fact]
    public async Task DeleteAttendance_不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateDeleteHandler(dbContext).HandleAsync(new DeleteAttendanceRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 列表 ==============================

    [Fact]
    public async Task GetAttendances_按员工类型与日期范围筛选_应按区间重叠命中并分页()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employeeA = NewEmployee("张三");
        var employeeB = NewEmployee("李四");
        dbContext.Employees.AddRange(employeeA, employeeB);
        var baseTime = DateTimeOffset.UtcNow;
        dbContext.Attendances.AddRange(
            new Attendance
            {
                Id = Guid.NewGuid(),
                EmployeeId = employeeA.Id,
                EmployeeName = employeeA.Name,
                Type = AttendanceType.Leave,
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2026, 9, 3),
                CreatedAt = baseTime,
                UpdatedAt = baseTime,
            },
            new Attendance
            {
                Id = Guid.NewGuid(),
                EmployeeId = employeeA.Id,
                EmployeeName = employeeA.Name,
                Type = AttendanceType.Overtime,
                StartDate = new DateOnly(2026, 9, 10),
                EndDate = new DateOnly(2026, 9, 10),
                CreatedAt = baseTime.AddMinutes(1),
                UpdatedAt = baseTime.AddMinutes(1),
            },
            new Attendance
            {
                Id = Guid.NewGuid(),
                EmployeeId = employeeB.Id,
                EmployeeName = employeeB.Name,
                Type = AttendanceType.Leave,
                StartDate = new DateOnly(2026, 8, 1),
                EndDate = new DateOnly(2026, 8, 2),
                CreatedAt = baseTime.AddMinutes(2),
                UpdatedAt = baseTime.AddMinutes(2),
            });
        await dbContext.SaveChangesAsync();

        var handler = CreateGetHandler(dbContext);

        // 员工 + 类型筛选
        var byEmployeeAndType = await handler.HandleAsync(new GetAttendancesRequest
        {
            EmployeeId = employeeA.Id,
            Type = (int)AttendanceType.Leave,
        });
        Assert.Equal(1, byEmployeeAndType.Total);
        Assert.Equal("张三", byEmployeeAndType.Items[0].EmployeeName);

        // 日期范围按区间重叠：09-03 与 09-01~09-03 有交集
        var byRange = await handler.HandleAsync(new GetAttendancesRequest
        {
            StartDate = new DateOnly(2026, 9, 3),
            EndDate = new DateOnly(2026, 9, 4),
        });
        Assert.Equal(1, byRange.Total);
        Assert.Equal(new DateOnly(2026, 9, 1), byRange.Items[0].StartDate);

        // 分页与总数（倒序：创建时间新的在前）
        var paged = await handler.HandleAsync(new GetAttendancesRequest { Page = 1, PageSize = 2 });
        Assert.Equal(3, paged.Total);
        Assert.Equal(2, paged.Items.Count);
        Assert.Equal("李四", paged.Items[0].EmployeeName);
    }
}
