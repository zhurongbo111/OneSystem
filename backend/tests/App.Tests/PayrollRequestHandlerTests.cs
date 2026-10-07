using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Payrolls.CreatePayroll;
using App.Core.Features.Payrolls.DeletePayroll;
using App.Core.Features.Payrolls.GeneratePayrolls;
using App.Core.Features.Payrolls.GetPayrolls;
using App.Core.Features.Payrolls.UpdatePayroll;
using App.Core.Features.Payrolls.UpdatePayrollStatus;
using App.Infrastructure;
using App.Infrastructure.Persistence;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// 工资单用例处理器测试（044-erp-hcm-payroll 任务 5.2 / 5.3）：
/// 实发计算与期间唯一、发放锁定、发放 / 反发放、批量生成（在职员工 + 跳过已存在 + 计数）。
/// </summary>
public class PayrollRequestHandlerTests
{
    private static Guid OperatorId { get; } = Guid.NewGuid();

    private static GetPayrollsRequestHandler CreateGetHandler(AppDbContext dbContext)
        => new(new PayrollRepository(dbContext));

    private static CreatePayrollRequestHandler CreateCreateHandler(AppDbContext dbContext)
        => new(
            new PayrollRepository(dbContext),
            new EmployeeRepository(dbContext),
            new UnitOfWork(dbContext),
            new StubCurrentUser(OperatorId),
            TestSupport.AuditLogger);

    private static UpdatePayrollRequestHandler CreateUpdateHandler(AppDbContext dbContext)
        => new(
            new PayrollRepository(dbContext),
            new UnitOfWork(dbContext),
            new StubCurrentUser(OperatorId),
            TestSupport.AuditLogger);

    private static UpdatePayrollStatusRequestHandler CreateStatusHandler(AppDbContext dbContext)
        => new(
            new PayrollRepository(dbContext),
            new UnitOfWork(dbContext),
            new StubCurrentUser(OperatorId),
            TestSupport.AuditLogger);

    private static DeletePayrollRequestHandler CreateDeleteHandler(AppDbContext dbContext)
        => new(
            new PayrollRepository(dbContext),
            new UnitOfWork(dbContext),
            TestSupport.AuditLogger);

    private static GeneratePayrollsRequestHandler CreateGenerateHandler(AppDbContext dbContext)
        => new(
            new PayrollRepository(dbContext),
            new UnitOfWork(dbContext),
            new StubCurrentUser(OperatorId),
            TestSupport.AuditLogger);

    /// <summary>构建员工实体（默认在职；可指定入职日期）</summary>
    private static Employee NewEmployee(
        string name = "张三",
        EmployeeStatus status = EmployeeStatus.Active,
        DateOnly? hireDate = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new Employee
        {
            Id = Guid.NewGuid(),
            EmployeeNo = $"E{Guid.NewGuid():N}"[..12],
            Name = name,
            HireDate = hireDate ?? new DateOnly(2026, 1, 1),
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>构建工资单实体</summary>
    private static Payroll NewPayroll(
        Employee employee,
        int year = 2026,
        int month = 9,
        decimal baseSalary = 10000m,
        PayrollStatus status = PayrollStatus.Draft)
    {
        var now = DateTimeOffset.UtcNow;
        return new Payroll
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            EmployeeName = employee.Name,
            Year = year,
            Month = month,
            BaseSalary = baseSalary,
            Allowance = 200m,
            Deduction = 50m,
            NetPay = baseSalary + 200m - 50m,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    // ============================== 新增 ==============================

    [Fact]
    public async Task CreatePayroll_合法请求_应计算实发并落草稿()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        var result = await CreateCreateHandler(dbContext).HandleAsync(new CreatePayrollRequest
        {
            EmployeeId = employee.Id,
            Year = 2026,
            Month = 9,
            BaseSalary = 1000m,
            Allowance = 200m,
            Deduction = 50m,
            Remark = " 试用期 ",
        });

        var saved = await dbContext.Payrolls.SingleAsync();
        Assert.Equal(1150m, saved.NetPay);
        Assert.Equal(PayrollStatus.Draft, saved.Status);
        Assert.Equal("张三", saved.EmployeeName);
        Assert.Equal("试用期", saved.Remark);
        Assert.Equal(OperatorId, saved.CreatedBy);
        Assert.Equal(1150m, result.NetPay);
        Assert.Equal("草稿", result.StatusText);
    }

    [Fact]
    public async Task CreatePayroll_津贴扣款缺省_实发等于基本工资()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        var result = await CreateCreateHandler(dbContext).HandleAsync(new CreatePayrollRequest
        {
            EmployeeId = employee.Id,
            Year = 2026,
            Month = 9,
            BaseSalary = 8000m,
        });

        Assert.Equal(8000m, result.NetPay);
        Assert.Equal(0m, result.Allowance);
        Assert.Equal(0m, result.Deduction);
    }

    [Fact]
    public async Task CreatePayroll_该员工该期间已存在_应抛业务异常40170()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        dbContext.Payrolls.Add(NewPayroll(employee));
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(dbContext).HandleAsync(new CreatePayrollRequest
            {
                EmployeeId = employee.Id,
                Year = 2026,
                Month = 9,
                BaseSalary = 1000m,
            }));

        Assert.Equal(ErrorCode.PayrollExists, ex.Code);
        Assert.Equal(1, await dbContext.Payrolls.CountAsync());
    }

    [Fact]
    public async Task CreatePayroll_员工不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateCreateHandler(dbContext).HandleAsync(new CreatePayrollRequest
            {
                EmployeeId = Guid.NewGuid(),
                Year = 2026,
                Month = 9,
                BaseSalary = 1000m,
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
        Assert.Equal(0, await dbContext.Payrolls.CountAsync());
    }

    // ============================== 编辑（发放锁定）==============================

    [Fact]
    public async Task UpdatePayroll_草稿_应重算实发并保留原期间与员工()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        var payroll = NewPayroll(employee, baseSalary: 10000m);
        dbContext.Payrolls.Add(payroll);
        await dbContext.SaveChangesAsync();

        var result = await CreateUpdateHandler(dbContext).HandleAsync(new UpdatePayrollRequest
        {
            Id = payroll.Id,
            BaseSalary = 12000m,
            Allowance = 300m,
            Deduction = 100m,
        });

        var saved = await dbContext.Payrolls.SingleAsync();
        Assert.Equal(12200m, saved.NetPay);
        Assert.Equal(2026, saved.Year);
        Assert.Equal(9, saved.Month);
        Assert.Equal(employee.Id, saved.EmployeeId);
        Assert.Equal(OperatorId, saved.UpdatedBy);
        Assert.Equal(12200m, result.NetPay);
    }

    [Fact]
    public async Task UpdatePayroll_备注留空_应按全量覆盖清空()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        var payroll = NewPayroll(employee);
        payroll.Remark = "原备注";
        dbContext.Payrolls.Add(payroll);
        await dbContext.SaveChangesAsync();

        var result = await CreateUpdateHandler(dbContext).HandleAsync(new UpdatePayrollRequest
        {
            Id = payroll.Id,
            BaseSalary = payroll.BaseSalary,
            Allowance = payroll.Allowance,
            Deduction = payroll.Deduction,
        });

        Assert.Null(result.Remark);
        Assert.Null((await dbContext.Payrolls.SingleAsync()).Remark);
    }

    [Fact]
    public async Task UpdatePayroll_已发放_应抛业务异常40171()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        var payroll = NewPayroll(employee, status: PayrollStatus.Paid);
        dbContext.Payrolls.Add(payroll);
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdatePayrollRequest
            {
                Id = payroll.Id,
                BaseSalary = 1m,
            }));

        Assert.Equal(ErrorCode.PayrollLocked, ex.Code);
        Assert.Equal(10000m, (await dbContext.Payrolls.SingleAsync()).BaseSalary);
    }

    [Fact]
    public async Task UpdatePayroll_不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateUpdateHandler(dbContext).HandleAsync(new UpdatePayrollRequest
            {
                Id = Guid.NewGuid(),
                BaseSalary = 1m,
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 发放 / 反发放 ==============================

    [Fact]
    public async Task UpdatePayrollStatus_发放_应由草稿转为已发放()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        var payroll = NewPayroll(employee);
        dbContext.Payrolls.Add(payroll);
        await dbContext.SaveChangesAsync();

        var result = await CreateStatusHandler(dbContext).HandleAsync(new UpdatePayrollStatusRequest
        {
            Id = payroll.Id,
            Status = (int)PayrollStatus.Paid,
        });

        Assert.Equal((int)PayrollStatus.Paid, result.Status);
        Assert.Equal("已发放", result.StatusText);
        Assert.Equal(PayrollStatus.Paid, (await dbContext.Payrolls.SingleAsync()).Status);
    }

    [Fact]
    public async Task UpdatePayrollStatus_反发放_应回到草稿()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        var payroll = NewPayroll(employee, status: PayrollStatus.Paid);
        dbContext.Payrolls.Add(payroll);
        await dbContext.SaveChangesAsync();

        var result = await CreateStatusHandler(dbContext).HandleAsync(new UpdatePayrollStatusRequest
        {
            Id = payroll.Id,
            Status = (int)PayrollStatus.Draft,
        });

        Assert.Equal((int)PayrollStatus.Draft, result.Status);
        Assert.Equal(PayrollStatus.Draft, (await dbContext.Payrolls.SingleAsync()).Status);
    }

    [Fact]
    public async Task UpdatePayrollStatus_状态未变化_应幂等返回且不改更新时间()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        var payroll = NewPayroll(employee);
        var originalUpdatedAt = payroll.UpdatedAt;
        dbContext.Payrolls.Add(payroll);
        await dbContext.SaveChangesAsync();

        var result = await CreateStatusHandler(dbContext).HandleAsync(new UpdatePayrollStatusRequest
        {
            Id = payroll.Id,
            Status = (int)PayrollStatus.Draft,
        });

        Assert.Equal((int)PayrollStatus.Draft, result.Status);
        Assert.Equal(originalUpdatedAt, (await dbContext.Payrolls.SingleAsync()).UpdatedAt);
    }

    [Fact]
    public async Task UpdatePayrollStatus_不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateStatusHandler(dbContext).HandleAsync(new UpdatePayrollStatusRequest
            {
                Id = Guid.NewGuid(),
                Status = (int)PayrollStatus.Paid,
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 删除 ==============================

    [Fact]
    public async Task DeletePayroll_草稿_应物理删除()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        var payroll = NewPayroll(employee);
        dbContext.Payrolls.Add(payroll);
        await dbContext.SaveChangesAsync();

        await CreateDeleteHandler(dbContext).HandleAsync(new DeletePayrollRequest { Id = payroll.Id });

        Assert.Empty(await dbContext.Payrolls.ToListAsync());
    }

    [Fact]
    public async Task DeletePayroll_已发放_应抛业务异常40171()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        var payroll = NewPayroll(employee, status: PayrollStatus.Paid);
        dbContext.Payrolls.Add(payroll);
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateDeleteHandler(dbContext).HandleAsync(new DeletePayrollRequest { Id = payroll.Id }));

        Assert.Equal(ErrorCode.PayrollLocked, ex.Code);
        Assert.Equal(1, await dbContext.Payrolls.CountAsync());
    }

    [Fact]
    public async Task DeletePayroll_不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateDeleteHandler(dbContext).HandleAsync(new DeletePayrollRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    // ============================== 批量生成 ==============================

    [Fact]
    public async Task GeneratePayrolls_为在职员工生成草稿并跳过已存在_计数应正确()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var existing = NewEmployee("已有");
        var fresh = NewEmployee("待生成");
        var resigned = NewEmployee("已离职", EmployeeStatus.Resigned);
        var future = NewEmployee("下月入职", hireDate: new DateOnly(2026, 10, 1));
        dbContext.Employees.AddRange(existing, fresh, resigned, future);
        // existing 已有 2026-09 工资单 → 应计入 skipped
        dbContext.Payrolls.Add(NewPayroll(existing));
        await dbContext.SaveChangesAsync();

        var result = await CreateGenerateHandler(dbContext).HandleAsync(new GeneratePayrollsRequest
        {
            Year = 2026,
            Month = 9,
        });

        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.Skipped);

        var created = await dbContext.Payrolls.SingleAsync(p => p.EmployeeId == fresh.Id);
        Assert.Equal(0m, created.BaseSalary);
        Assert.Equal(0m, created.NetPay);
        Assert.Equal(PayrollStatus.Draft, created.Status);
        Assert.Equal(2026, created.Year);
        Assert.Equal(9, created.Month);
        Assert.Equal(OperatorId, created.CreatedBy);

        // 离职员工与入职晚于期间末的员工均不生成
        Assert.False(await dbContext.Payrolls.AnyAsync(p => p.EmployeeId == resigned.Id));
        Assert.False(await dbContext.Payrolls.AnyAsync(p => p.EmployeeId == future.Id));
        Assert.Equal(2, await dbContext.Payrolls.CountAsync());
    }

    [Fact]
    public async Task GeneratePayrolls_重复执行_应幂等且全部跳过()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employee = NewEmployee();
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        var handler = CreateGenerateHandler(dbContext);
        var first = await handler.HandleAsync(new GeneratePayrollsRequest { Year = 2026, Month = 9 });
        var second = await handler.HandleAsync(new GeneratePayrollsRequest { Year = 2026, Month = 9 });

        Assert.Equal(1, first.Created);
        Assert.Equal(0, first.Skipped);
        Assert.Equal(0, second.Created);
        Assert.Equal(1, second.Skipped);
        Assert.Equal(1, await dbContext.Payrolls.CountAsync());
    }

    [Fact]
    public async Task GeneratePayrolls_无可生成员工_应返回零计数()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var result = await CreateGenerateHandler(dbContext).HandleAsync(new GeneratePayrollsRequest
        {
            Year = 2026,
            Month = 9,
        });

        Assert.Equal(0, result.Created);
        Assert.Equal(0, result.Skipped);
    }

    // ============================== 列表 ==============================

    [Fact]
    public async Task GetPayrolls_按期间与状态筛选_应分页返回()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var employeeA = NewEmployee("张三");
        var employeeB = NewEmployee("李四");
        dbContext.Employees.AddRange(employeeA, employeeB);
        dbContext.Payrolls.AddRange(
            NewPayroll(employeeA, 2026, 9, 10000m),
            NewPayroll(employeeB, 2026, 9, 9000m, PayrollStatus.Paid),
            NewPayroll(employeeA, 2026, 8, 8000m));
        await dbContext.SaveChangesAsync();

        var handler = CreateGetHandler(dbContext);

        var byPeriod = await handler.HandleAsync(new GetPayrollsRequest { Year = 2026, Month = 9 });
        Assert.Equal(2, byPeriod.Total);

        var byStatus = await handler.HandleAsync(new GetPayrollsRequest
        {
            Year = 2026,
            Month = 9,
            Status = (int)PayrollStatus.Paid,
        });
        Assert.Equal(1, byStatus.Total);
        Assert.Equal("李四", byStatus.Items[0].EmployeeName);
        Assert.Equal("已发放", byStatus.Items[0].StatusText);

        var byEmployee = await handler.HandleAsync(new GetPayrollsRequest { EmployeeId = employeeA.Id });
        Assert.Equal(2, byEmployee.Total);

        var paged = await handler.HandleAsync(new GetPayrollsRequest { Page = 1, PageSize = 2 });
        Assert.Equal(3, paged.Total);
        Assert.Equal(2, paged.Items.Count);
    }
}
