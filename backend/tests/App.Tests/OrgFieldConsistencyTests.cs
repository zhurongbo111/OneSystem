using App.Core.Entities;
using App.Core.Features.Departments.CreateDepartment;
using App.Core.Features.Departments.UpdateDepartment;
using App.Core.Features.Employees.CreateEmployee;
using App.Core.Features.Employees.ExportEmployees;
using App.Core.Features.Employees.GetEmployees;
using App.Core.Features.Employees.UpdateEmployee;
using App.Core.Features.Positions.CreatePosition;
using App.Core.Features.Positions.GetPositions;
using App.Core.Features.Positions.UpdatePosition;
using App.Infrastructure;

namespace App.Tests;

/// <summary>
/// 组织人事字段约束一致性测试（specs/030-erp-org-employee tasks.md 6.5）：
/// ① 三实体 EF 实际列长度 == 对应常量；② 岗位 / 员工查询关键词上限 == 实际匹配列长；
/// ③ 员工手机 / 邮箱 / 日期边界与枚举取值在创建 / 编辑两处一致。
/// </summary>
public class OrgFieldConsistencyTests
{
    private static int GetMaxLength<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetMaxLength()!.Value;

    // ============================== EF 模型 ←→ 常量 ==============================

    [Fact]
    public void EF模型_Departments表列长度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(DepartmentFieldConstraints.CodeMaxLength, GetMaxLength<Department>(dbContext, nameof(Department.Code)));
        Assert.Equal(DepartmentFieldConstraints.NameMaxLength, GetMaxLength<Department>(dbContext, nameof(Department.Name)));
        Assert.Equal(DepartmentFieldConstraints.RemarkMaxLength, GetMaxLength<Department>(dbContext, nameof(Department.Remark)));
    }

    [Fact]
    public void EF模型_Positions表列长度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(PositionFieldConstraints.CodeMaxLength, GetMaxLength<Position>(dbContext, nameof(Position.Code)));
        Assert.Equal(PositionFieldConstraints.NameMaxLength, GetMaxLength<Position>(dbContext, nameof(Position.Name)));
        Assert.Equal(PositionFieldConstraints.RemarkMaxLength, GetMaxLength<Position>(dbContext, nameof(Position.Remark)));
    }

    [Fact]
    public void EF模型_Employees表列长度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(EmployeeFieldConstraints.NoMaxLength, GetMaxLength<Employee>(dbContext, nameof(Employee.EmployeeNo)));
        Assert.Equal(EmployeeFieldConstraints.NameMaxLength, GetMaxLength<Employee>(dbContext, nameof(Employee.Name)));
        Assert.Equal(EmployeeFieldConstraints.PhoneMaxLength, GetMaxLength<Employee>(dbContext, nameof(Employee.Phone)));
        Assert.Equal(EmployeeFieldConstraints.EmailMaxLength, GetMaxLength<Employee>(dbContext, nameof(Employee.Email)));
        Assert.Equal(EmployeeFieldConstraints.RemarkMaxLength, GetMaxLength<Employee>(dbContext, nameof(Employee.Remark)));
    }

    // ============================== 查询关键词上限 == 实际匹配列 ==============================

    [Fact]
    public void 岗位查询关键词长度_应不超过名称列长()
    {
        var ok = new string('a', PositionFieldConstraints.NameMaxLength);
        var tooLong = new string('a', PositionFieldConstraints.NameMaxLength + 1);

        Assert.True(new GetPositionsRequestValidator()
            .Validate(new GetPositionsRequest { Page = 1, PageSize = 20, Keyword = ok }).IsValid);
        Assert.False(new GetPositionsRequestValidator()
            .Validate(new GetPositionsRequest { Page = 1, PageSize = 20, Keyword = tooLong }).IsValid);
    }

    [Fact]
    public void 员工查询与导出关键词长度_应不超过姓名列长()
    {
        var ok = new string('a', EmployeeFieldConstraints.NameMaxLength);
        var tooLong = new string('a', EmployeeFieldConstraints.NameMaxLength + 1);

        Assert.True(new GetEmployeesRequestValidator()
            .Validate(new GetEmployeesRequest { Page = 1, PageSize = 20, Keyword = ok }).IsValid);
        Assert.False(new GetEmployeesRequestValidator()
            .Validate(new GetEmployeesRequest { Page = 1, PageSize = 20, Keyword = tooLong }).IsValid);

        Assert.True(new ExportEmployeesRequestValidator()
            .Validate(new ExportEmployeesRequest { Page = 1, PageSize = 20, Keyword = ok }).IsValid);
        Assert.False(new ExportEmployeesRequestValidator()
            .Validate(new ExportEmployeesRequest { Page = 1, PageSize = 20, Keyword = tooLong }).IsValid);
    }

    // ============================== 员工手机 / 邮箱 / 日期 / 枚举（创建与编辑同源）==============================

    [Fact]
    public void 员工工号长度_创建应引用常量而编辑不含工号字段()
    {
        var ok = new string('a', EmployeeFieldConstraints.NoMaxLength);
        var tooLong = new string('a', EmployeeFieldConstraints.NoMaxLength + 1);

        Assert.True(ValidateCreate(new CreateEmployeeRequest { EmployeeNo = ok, Name = "张三", HireDate = new DateOnly(2026, 1, 1) }));
        Assert.False(ValidateCreate(new CreateEmployeeRequest { EmployeeNo = tooLong, Name = "张三", HireDate = new DateOnly(2026, 1, 1) }));

        // 工号创建后不可改：编辑请求类型上不存在该字段（编译期保证），此处守护字段不被误加回
        Assert.Null(typeof(UpdateEmployeeRequest).GetProperty("EmployeeNo"));
    }

    [Fact]
    public void 员工姓名长度_创建与编辑应一致且不超过数据库列长度()
    {
        var ok = new string('名', EmployeeFieldConstraints.NameMaxLength);
        var tooLong = new string('名', EmployeeFieldConstraints.NameMaxLength + 1);

        Assert.True(ValidateCreate(new CreateEmployeeRequest { EmployeeNo = "E0001", Name = ok, HireDate = new DateOnly(2026, 1, 1) }));
        Assert.True(ValidateUpdate(new UpdateEmployeeRequest { Id = Guid.NewGuid(), Name = ok, HireDate = new DateOnly(2026, 1, 1) }));

        Assert.False(ValidateCreate(new CreateEmployeeRequest { EmployeeNo = "E0001", Name = tooLong, HireDate = new DateOnly(2026, 1, 1) }));
        Assert.False(ValidateUpdate(new UpdateEmployeeRequest { Id = Guid.NewGuid(), Name = tooLong, HireDate = new DateOnly(2026, 1, 1) }));
    }

    [Fact]
    public void 员工手机号_创建与编辑应一致_且接受合法值拒绝超长或非法值()
    {
        const string legal = "13800138000";
        var tooLong = new string('1', EmployeeFieldConstraints.PhoneMaxLength + 1);

        Assert.True(ValidateCreate(new CreateEmployeeRequest { EmployeeNo = "E0001", Name = "张三", HireDate = new DateOnly(2026, 1, 1), Phone = legal }));
        Assert.True(ValidateUpdate(new UpdateEmployeeRequest { Id = Guid.NewGuid(), Name = "张三", HireDate = new DateOnly(2026, 1, 1), Phone = legal }));

        // 长度上限虽为 PhoneMaxLength，但格式正则已把合法值限定为 11 位；超长与非法格式都必须被拒绝
        Assert.False(ValidateCreate(new CreateEmployeeRequest { EmployeeNo = "E0001", Name = "张三", HireDate = new DateOnly(2026, 1, 1), Phone = tooLong }));
        Assert.False(ValidateUpdate(new UpdateEmployeeRequest { Id = Guid.NewGuid(), Name = "张三", HireDate = new DateOnly(2026, 1, 1), Phone = tooLong }));
        Assert.False(ValidateCreate(new CreateEmployeeRequest { EmployeeNo = "E0001", Name = "张三", HireDate = new DateOnly(2026, 1, 1), Phone = "12345" }));
        Assert.False(ValidateUpdate(new UpdateEmployeeRequest { Id = Guid.NewGuid(), Name = "张三", HireDate = new DateOnly(2026, 1, 1), Phone = "12345" }));
    }

    [Fact]
    public void 员工邮箱长度_创建与编辑应一致且不超过数据库列长度()
    {
        const string suffix = "@example.com";
        var ok = new string('a', EmployeeFieldConstraints.EmailMaxLength - suffix.Length) + suffix;
        var tooLong = new string('a', EmployeeFieldConstraints.EmailMaxLength - suffix.Length + 1) + suffix;

        Assert.True(ValidateCreate(new CreateEmployeeRequest { EmployeeNo = "E0001", Name = "张三", HireDate = new DateOnly(2026, 1, 1), Email = ok }));
        Assert.True(ValidateUpdate(new UpdateEmployeeRequest { Id = Guid.NewGuid(), Name = "张三", HireDate = new DateOnly(2026, 1, 1), Email = ok }));

        Assert.False(ValidateCreate(new CreateEmployeeRequest { EmployeeNo = "E0001", Name = "张三", HireDate = new DateOnly(2026, 1, 1), Email = tooLong }));
        Assert.False(ValidateUpdate(new UpdateEmployeeRequest { Id = Guid.NewGuid(), Name = "张三", HireDate = new DateOnly(2026, 1, 1), Email = tooLong }));
    }

    [Fact]
    public void 员工入职离职日期_创建与编辑应一致_离职早于入职应拒绝()
    {
        var hireDate = new DateOnly(2026, 5, 1);

        Assert.True(ValidateCreate(new CreateEmployeeRequest { EmployeeNo = "E0001", Name = "张三", HireDate = hireDate, ResignDate = hireDate }));
        Assert.True(ValidateUpdate(new UpdateEmployeeRequest { Id = Guid.NewGuid(), Name = "张三", HireDate = hireDate, ResignDate = hireDate }));

        Assert.False(ValidateCreate(new CreateEmployeeRequest { EmployeeNo = "E0001", Name = "张三", HireDate = hireDate, ResignDate = hireDate.AddDays(-1) }));
        Assert.False(ValidateUpdate(new UpdateEmployeeRequest { Id = Guid.NewGuid(), Name = "张三", HireDate = hireDate, ResignDate = hireDate.AddDays(-1) }));

        // 入职日期为默认值（未填）一律拒绝
        Assert.False(ValidateCreate(new CreateEmployeeRequest { EmployeeNo = "E0001", Name = "张三" }));
        Assert.False(ValidateUpdate(new UpdateEmployeeRequest { Id = Guid.NewGuid(), Name = "张三" }));
    }

    [Fact]
    public void 员工枚举取值_创建与编辑应一致_非法性别或状态应拒绝()
    {
        Assert.True(ValidateCreate(new CreateEmployeeRequest { EmployeeNo = "E0001", Name = "张三", HireDate = new DateOnly(2026, 1, 1), Gender = (int)Gender.Female, Status = (int)EmployeeStatus.Resigned }));
        Assert.True(ValidateUpdate(new UpdateEmployeeRequest { Id = Guid.NewGuid(), Name = "张三", HireDate = new DateOnly(2026, 1, 1), Gender = (int)Gender.Female, Status = (int)EmployeeStatus.Resigned }));

        Assert.False(ValidateCreate(new CreateEmployeeRequest { EmployeeNo = "E0001", Name = "张三", HireDate = new DateOnly(2026, 1, 1), Gender = 3 }));
        Assert.False(ValidateUpdate(new UpdateEmployeeRequest { Id = Guid.NewGuid(), Name = "张三", HireDate = new DateOnly(2026, 1, 1), Gender = 3 }));
        Assert.False(ValidateCreate(new CreateEmployeeRequest { EmployeeNo = "E0001", Name = "张三", HireDate = new DateOnly(2026, 1, 1), Status = 2 }));
        Assert.False(ValidateUpdate(new UpdateEmployeeRequest { Id = Guid.NewGuid(), Name = "张三", HireDate = new DateOnly(2026, 1, 1), Status = 2 }));
    }

    // ============================== 部门 / 岗位编码名称长度（创建与编辑同源）==============================

    [Fact]
    public void 部门编码名称排序_创建与编辑应引用同一常量区间()
    {
        var codeOk = new string('a', DepartmentFieldConstraints.CodeMaxLength);
        var nameOk = new string('名', DepartmentFieldConstraints.NameMaxLength);

        Assert.True(new CreateDepartmentRequestValidator().Validate(new CreateDepartmentRequest
        {
            Code = codeOk,
            Name = nameOk,
            SortOrder = DepartmentFieldConstraints.SortOrderMaxValue,
        }).IsValid);

        Assert.False(new CreateDepartmentRequestValidator().Validate(new CreateDepartmentRequest
        {
            Code = codeOk,
            Name = nameOk,
            SortOrder = DepartmentFieldConstraints.SortOrderMaxValue + 1,
        }).IsValid);

        Assert.True(new UpdateDepartmentRequestValidator().Validate(new UpdateDepartmentRequest
        {
            Id = Guid.NewGuid(),
            Code = codeOk,
            Name = nameOk,
            SortOrder = DepartmentFieldConstraints.SortOrderMinValue,
        }).IsValid);
    }

    [Fact]
    public void 岗位编码名称长度_创建与编辑应一致()
    {
        var codeOk = new string('a', PositionFieldConstraints.CodeMaxLength);
        var nameOk = new string('名', PositionFieldConstraints.NameMaxLength);
        var nameTooLong = new string('名', PositionFieldConstraints.NameMaxLength + 1);

        Assert.True(new CreatePositionRequestValidator()
            .Validate(new CreatePositionRequest { Code = codeOk, Name = nameOk }).IsValid);
        Assert.True(new UpdatePositionRequestValidator()
            .Validate(new UpdatePositionRequest { Id = Guid.NewGuid(), Code = codeOk, Name = nameOk }).IsValid);

        Assert.False(new CreatePositionRequestValidator()
            .Validate(new CreatePositionRequest { Code = codeOk, Name = nameTooLong }).IsValid);
        Assert.False(new UpdatePositionRequestValidator()
            .Validate(new UpdatePositionRequest { Id = Guid.NewGuid(), Code = codeOk, Name = nameTooLong }).IsValid);
    }

    private static bool ValidateCreate(CreateEmployeeRequest request)
        => new CreateEmployeeRequestValidator().Validate(request).IsValid;

    private static bool ValidateUpdate(UpdateEmployeeRequest request)
        => new UpdateEmployeeRequestValidator().Validate(request).IsValid;
}
