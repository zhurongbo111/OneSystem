using App.Core.Entities;
using App.Core.Features.Attendances.CreateAttendance;
using App.Core.Features.Attendances.GetAttendances;
using App.Core.Features.Attendances.UpdateAttendance;
using App.Core.Features.Payrolls.CreatePayroll;
using App.Core.Features.Payrolls.GeneratePayrolls;
using App.Core.Features.Payrolls.GetPayrolls;
using App.Core.Features.Payrolls.UpdatePayroll;
using App.Core.Features.Payrolls.UpdatePayrollStatus;
using App.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// 考勤与薪酬字段约束一致性测试（specs/044-erp-hcm-payroll tasks.md 5.4）：
/// ① 两实体 EF 实际列长 / 列类型 == 对应常量；
/// ② 日期与枚举边界、金额区间在创建 / 编辑两处同源；
/// ③ 列表分页边界与期间区间。
/// </summary>
public class HcmPayrollFieldConsistencyTests
{
    private static int GetMaxLength<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetMaxLength()!.Value;

    private static string? GetColumnType<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetColumnType();

    /// <summary>
    /// 构建「关系型提供程序」模型上下文：仅用于读取列类型（InMemory 提供程序不支持列类型映射，且**不建立连接**）
    /// </summary>
    private static AppDbContext CreateRelationalDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=model-only")
            .Options;
        return new AppDbContext(options);
    }

    // ============================== EF 模型 ←→ 常量 ==============================

    [Fact]
    public void EF模型_Attendances表列长_应等于字段约束常量()
    {
        using var dbContext = CreateRelationalDbContext();

        Assert.Equal(EmployeeFieldConstraints.NameMaxLength, GetMaxLength<Attendance>(dbContext, nameof(Attendance.EmployeeName)));
        Assert.Equal(AttendanceFieldConstraints.RemarkMaxLength, GetMaxLength<Attendance>(dbContext, nameof(Attendance.Remark)));
        Assert.Equal("date", GetColumnType<Attendance>(dbContext, nameof(Attendance.StartDate)));
        Assert.Equal("date", GetColumnType<Attendance>(dbContext, nameof(Attendance.EndDate)));
    }

    [Fact]
    public void EF模型_Payrolls表列长与金额精度_应等于字段约束常量()
    {
        using var dbContext = CreateRelationalDbContext();

        Assert.Equal(EmployeeFieldConstraints.NameMaxLength, GetMaxLength<Payroll>(dbContext, nameof(Payroll.EmployeeName)));
        Assert.Equal(PayrollFieldConstraints.RemarkMaxLength, GetMaxLength<Payroll>(dbContext, nameof(Payroll.Remark)));
        Assert.Equal("numeric(18,2)", GetColumnType<Payroll>(dbContext, nameof(Payroll.BaseSalary)));
        Assert.Equal("numeric(18,2)", GetColumnType<Payroll>(dbContext, nameof(Payroll.Allowance)));
        Assert.Equal("numeric(18,2)", GetColumnType<Payroll>(dbContext, nameof(Payroll.Deduction)));
        Assert.Equal("numeric(18,2)", GetColumnType<Payroll>(dbContext, nameof(Payroll.NetPay)));
    }

    // ============================== 考勤：日期 / 枚举 / 事由长度 ==============================

    [Fact]
    public void 考勤日期边界_创建与编辑应一致_结束早于起始应拒绝()
    {
        var start = new DateOnly(2026, 9, 21);

        Assert.True(ValidateCreateAttendance(new CreateAttendanceRequest
        {
            EmployeeId = Guid.NewGuid(),
            Type = (int)AttendanceType.Leave,
            StartDate = start,
            EndDate = start,
        }));
        Assert.True(ValidateUpdateAttendance(new UpdateAttendanceRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = Guid.NewGuid(),
            Type = (int)AttendanceType.Leave,
            StartDate = start,
            EndDate = start.AddDays(1),
        }));

        Assert.False(ValidateCreateAttendance(new CreateAttendanceRequest
        {
            EmployeeId = Guid.NewGuid(),
            Type = (int)AttendanceType.Leave,
            StartDate = start,
            EndDate = start.AddDays(-1),
        }));
        Assert.False(ValidateUpdateAttendance(new UpdateAttendanceRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = Guid.NewGuid(),
            Type = (int)AttendanceType.Leave,
            StartDate = start,
            EndDate = start.AddDays(-1),
        }));

        // 起始日 / 结束日未填（默认值）一律拒绝
        Assert.False(ValidateCreateAttendance(new CreateAttendanceRequest { EmployeeId = Guid.NewGuid() }));
        Assert.False(ValidateUpdateAttendance(new UpdateAttendanceRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = Guid.NewGuid(),
            EndDate = start,
        }));
    }

    [Fact]
    public void 考勤类型枚举_创建与编辑应一致_非法值应拒绝()
    {
        var start = new DateOnly(2026, 9, 21);

        Assert.True(ValidateCreateAttendance(new CreateAttendanceRequest
        {
            EmployeeId = Guid.NewGuid(),
            Type = (int)AttendanceType.Overtime,
            StartDate = start,
            EndDate = start,
        }));
        Assert.True(ValidateUpdateAttendance(new UpdateAttendanceRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = Guid.NewGuid(),
            Type = (int)AttendanceType.Overtime,
            StartDate = start,
            EndDate = start,
        }));

        Assert.False(ValidateCreateAttendance(new CreateAttendanceRequest
        {
            EmployeeId = Guid.NewGuid(),
            Type = 2,
            StartDate = start,
            EndDate = start,
        }));
        Assert.False(ValidateUpdateAttendance(new UpdateAttendanceRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = Guid.NewGuid(),
            Type = 2,
            StartDate = start,
            EndDate = start,
        }));
    }

    [Fact]
    public void 考勤事由长度_创建与编辑应一致且不超过数据库列长()
    {
        var start = new DateOnly(2026, 9, 21);
        var ok = new string('备', AttendanceFieldConstraints.RemarkMaxLength);
        var tooLong = new string('备', AttendanceFieldConstraints.RemarkMaxLength + 1);

        Assert.True(ValidateCreateAttendance(new CreateAttendanceRequest
        {
            EmployeeId = Guid.NewGuid(),
            StartDate = start,
            EndDate = start,
            Remark = ok,
        }));
        Assert.True(ValidateUpdateAttendance(new UpdateAttendanceRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = Guid.NewGuid(),
            StartDate = start,
            EndDate = start,
            Remark = ok,
        }));

        Assert.False(ValidateCreateAttendance(new CreateAttendanceRequest
        {
            EmployeeId = Guid.NewGuid(),
            StartDate = start,
            EndDate = start,
            Remark = tooLong,
        }));
        Assert.False(ValidateUpdateAttendance(new UpdateAttendanceRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = Guid.NewGuid(),
            StartDate = start,
            EndDate = start,
            Remark = tooLong,
        }));
    }

    // ============================== 工资单：金额 / 期间 / 备注 ==============================

    [Fact]
    public void 工资单金额区间_创建与编辑应一致_上界取常量()
    {
        var max = PayrollFieldConstraints.AmountMaxValue;
        var over = max + 0.01m;

        Assert.True(new CreatePayrollRequestValidator().Validate(new CreatePayrollRequest
        {
            EmployeeId = Guid.NewGuid(),
            Year = 2026,
            Month = 9,
            BaseSalary = max,
            Allowance = max,
            Deduction = max,
        }).IsValid);
        Assert.True(new UpdatePayrollRequestValidator().Validate(new UpdatePayrollRequest
        {
            Id = Guid.NewGuid(),
            BaseSalary = max,
            Allowance = max,
            Deduction = max,
        }).IsValid);

        Assert.False(new CreatePayrollRequestValidator().Validate(new CreatePayrollRequest
        {
            EmployeeId = Guid.NewGuid(),
            Year = 2026,
            Month = 9,
            BaseSalary = over,
        }).IsValid);
        Assert.False(new CreatePayrollRequestValidator().Validate(new CreatePayrollRequest
        {
            EmployeeId = Guid.NewGuid(),
            Year = 2026,
            Month = 9,
            BaseSalary = -0.01m,
        }).IsValid);
        Assert.False(new UpdatePayrollRequestValidator().Validate(new UpdatePayrollRequest
        {
            Id = Guid.NewGuid(),
            Deduction = over,
        }).IsValid);
        Assert.False(new UpdatePayrollRequestValidator().Validate(new UpdatePayrollRequest
        {
            Id = Guid.NewGuid(),
            Allowance = -0.01m,
        }).IsValid);
    }

    [Fact]
    public void 工资单期间区间_新增批量生成与筛选应同源()
    {
        Assert.True(new CreatePayrollRequestValidator().Validate(new CreatePayrollRequest
        {
            EmployeeId = Guid.NewGuid(),
            Year = 2000,
            Month = 1,
            BaseSalary = 1m,
        }).IsValid);
        Assert.True(new CreatePayrollRequestValidator().Validate(new CreatePayrollRequest
        {
            EmployeeId = Guid.NewGuid(),
            Year = 2100,
            Month = 12,
            BaseSalary = 1m,
        }).IsValid);
        Assert.False(new CreatePayrollRequestValidator().Validate(new CreatePayrollRequest
        {
            EmployeeId = Guid.NewGuid(),
            Year = 1999,
            Month = 12,
            BaseSalary = 1m,
        }).IsValid);
        Assert.False(new CreatePayrollRequestValidator().Validate(new CreatePayrollRequest
        {
            EmployeeId = Guid.NewGuid(),
            Year = 2101,
            Month = 1,
            BaseSalary = 1m,
        }).IsValid);
        Assert.False(new CreatePayrollRequestValidator().Validate(new CreatePayrollRequest
        {
            EmployeeId = Guid.NewGuid(),
            Year = 2026,
            Month = 13,
            BaseSalary = 1m,
        }).IsValid);
        Assert.False(new CreatePayrollRequestValidator().Validate(new CreatePayrollRequest
        {
            EmployeeId = Guid.NewGuid(),
            Year = 2026,
            Month = 0,
            BaseSalary = 1m,
        }).IsValid);

        Assert.True(new GeneratePayrollsRequestValidator()
            .Validate(new GeneratePayrollsRequest { Year = 2026, Month = 9 }).IsValid);
        Assert.False(new GeneratePayrollsRequestValidator()
            .Validate(new GeneratePayrollsRequest { Year = 2026, Month = 13 }).IsValid);

        Assert.True(new GetPayrollsRequestValidator()
            .Validate(new GetPayrollsRequest { Page = 1, PageSize = 100, Year = 2026, Month = 12 }).IsValid);
        Assert.False(new GetPayrollsRequestValidator()
            .Validate(new GetPayrollsRequest { Page = 1, PageSize = 100, Month = 13 }).IsValid);
    }

    [Fact]
    public void 工资单备注长度_创建与编辑应一致且不超过数据库列长()
    {
        var ok = new string('备', PayrollFieldConstraints.RemarkMaxLength);
        var tooLong = new string('备', PayrollFieldConstraints.RemarkMaxLength + 1);

        Assert.True(new CreatePayrollRequestValidator().Validate(new CreatePayrollRequest
        {
            EmployeeId = Guid.NewGuid(),
            Year = 2026,
            Month = 9,
            BaseSalary = 1m,
            Remark = ok,
        }).IsValid);
        Assert.True(new UpdatePayrollRequestValidator()
            .Validate(new UpdatePayrollRequest { Id = Guid.NewGuid(), Remark = ok }).IsValid);

        Assert.False(new CreatePayrollRequestValidator().Validate(new CreatePayrollRequest
        {
            EmployeeId = Guid.NewGuid(),
            Year = 2026,
            Month = 9,
            BaseSalary = 1m,
            Remark = tooLong,
        }).IsValid);
        Assert.False(new UpdatePayrollRequestValidator()
            .Validate(new UpdatePayrollRequest { Id = Guid.NewGuid(), Remark = tooLong }).IsValid);
    }

    [Fact]
    public void 工资单状态枚举_发放与筛选应一致_非法值应拒绝()
    {
        Assert.True(new UpdatePayrollStatusRequestValidator()
            .Validate(new UpdatePayrollStatusRequest { Id = Guid.NewGuid(), Status = (int)PayrollStatus.Paid }).IsValid);
        Assert.True(new UpdatePayrollStatusRequestValidator()
            .Validate(new UpdatePayrollStatusRequest { Id = Guid.NewGuid(), Status = (int)PayrollStatus.Draft }).IsValid);
        Assert.False(new UpdatePayrollStatusRequestValidator()
            .Validate(new UpdatePayrollStatusRequest { Id = Guid.NewGuid(), Status = 2 }).IsValid);

        Assert.True(new GetPayrollsRequestValidator()
            .Validate(new GetPayrollsRequest { Page = 1, PageSize = 20, Status = (int)PayrollStatus.Paid }).IsValid);
        Assert.False(new GetPayrollsRequestValidator()
            .Validate(new GetPayrollsRequest { Page = 1, PageSize = 20, Status = 2 }).IsValid);
    }

    // ============================== 列表分页边界 ==============================

    [Fact]
    public void 考勤与工资单列表分页边界_应一致()
    {
        Assert.True(new GetAttendancesRequestValidator()
            .Validate(new GetAttendancesRequest { Page = 1, PageSize = 1 }).IsValid);
        Assert.True(new GetAttendancesRequestValidator()
            .Validate(new GetAttendancesRequest { Page = 1, PageSize = 100 }).IsValid);
        Assert.False(new GetAttendancesRequestValidator()
            .Validate(new GetAttendancesRequest { Page = 0, PageSize = 20 }).IsValid);
        Assert.False(new GetAttendancesRequestValidator()
            .Validate(new GetAttendancesRequest { Page = 1, PageSize = 101 }).IsValid);

        Assert.True(new GetPayrollsRequestValidator()
            .Validate(new GetPayrollsRequest { Page = 1, PageSize = 1 }).IsValid);
        Assert.False(new GetPayrollsRequestValidator()
            .Validate(new GetPayrollsRequest { Page = 1, PageSize = 101 }).IsValid);
        Assert.False(new GetPayrollsRequestValidator()
            .Validate(new GetPayrollsRequest { Page = 0, PageSize = 20 }).IsValid);
    }

    [Fact]
    public void 考勤列表日期范围_结束早于起始应拒绝()
    {
        Assert.True(new GetAttendancesRequestValidator().Validate(new GetAttendancesRequest
        {
            Page = 1,
            PageSize = 20,
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 9, 30),
        }).IsValid);

        Assert.False(new GetAttendancesRequestValidator().Validate(new GetAttendancesRequest
        {
            Page = 1,
            PageSize = 20,
            StartDate = new DateOnly(2026, 9, 30),
            EndDate = new DateOnly(2026, 9, 1),
        }).IsValid);
    }

    private static bool ValidateCreateAttendance(CreateAttendanceRequest request)
        => new CreateAttendanceRequestValidator().Validate(request).IsValid;

    private static bool ValidateUpdateAttendance(UpdateAttendanceRequest request)
        => new UpdateAttendanceRequestValidator().Validate(request).IsValid;
}
