using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Exports;
using App.Core.Features.Employees.ExportEmployees;
using App.Infrastructure.Exports;

using ClosedXML.Excel;

namespace App.Tests;

/// <summary>
/// 员工列表导出用例测试（erp-export）：筛选透传、按上限 + 1 取数、超限 40000、列头与列表一致
/// </summary>
public class EmployeeExportRequestHandlerTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 9, 22, 10, 30, 0, TimeSpan.Zero);

    private static (ExportEmployeesRequestHandler Handler, RecordingEmployeeRepository Employees, RecordingUserRepository Users) CreateHandler()
    {
        var employees = new RecordingEmployeeRepository();
        var users = new RecordingUserRepository();
        return (new ExportEmployeesRequestHandler(employees, users, new ClosedXmlExcelExporter()), employees, users);
    }

    private static EmployeeListItem NewItem(Guid? createdBy = null)
        => new()
        {
            Id = Guid.NewGuid(),
            EmployeeNo = "E0001",
            Name = "张三",
            Gender = Gender.Male,
            Phone = "13800138000",
            DepartmentId = Guid.NewGuid(),
            DepartmentName = "研发中心",
            PositionId = Guid.NewGuid(),
            PositionName = "后端工程师",
            HireDate = new DateOnly(2026, 1, 1),
            ResignDate = null,
            Status = EmployeeStatus.Active,
            UserId = null,
            UserDisplayName = null,
            CreatedAt = BaseTime,
            CreatedBy = createdBy,
        };

    [Fact]
    public async Task 导出员工_按上限取全量并透传筛选_创建人换显示名()
    {
        var creatorId = Guid.NewGuid();
        var (handler, employees, users) = CreateHandler();
        employees.Items = [NewItem(creatorId)];
        users.DisplayNames[creatorId] = "管理员";

        var departmentId = Guid.NewGuid();
        var positionId = Guid.NewGuid();
        var result = await handler.HandleAsync(new ExportEmployeesRequest
        {
            Keyword = "张",
            DepartmentId = departmentId,
            PositionId = positionId,
            Status = EmployeeStatus.Active,
            Page = 3,
            PageSize = 50,
        });

        // 分页参数不参与取数：恒取上限 + 1（用于超限判定）
        var args = employees.LastExportArgs!.Value;
        Assert.Equal("张", args.Keyword);
        Assert.Equal(departmentId, args.DepartmentId);
        Assert.Equal(positionId, args.PositionId);
        Assert.Equal(EmployeeStatus.Active, args.Status);
        Assert.Equal(ExportFieldConstraints.MaxRows + 1, args.Limit);
        Assert.Equal([creatorId], users.LastDisplayNameIds);

        Assert.StartsWith("员工档案_", result.FileName);
        Assert.EndsWith(".xlsx", result.FileName);

        using var workbook = new XLWorkbook(new MemoryStream(result.Content));
        var sheet = workbook.Worksheet("员工档案");
        Assert.Equal(
            ["工号", "姓名", "性别", "手机号", "部门", "岗位", "入职日期", "状态", "关联账号", "创建人"],
            sheet.Row(1).Cells(1, 10).Select(c => c.GetString()).ToList());
        Assert.Equal("E0001", sheet.Cell(2, 1).GetString());
        Assert.Equal("男", sheet.Cell(2, 3).GetString());
        Assert.Equal("在职", sheet.Cell(2, 8).GetString());
        Assert.Equal("管理员", sheet.Cell(2, 10).GetString());
    }

    [Fact]
    public async Task 导出员工_超过上限_应抛业务异常40000()
    {
        var (handler, employees, _) = CreateHandler();
        employees.Items = Enumerable
            .Range(0, ExportFieldConstraints.MaxRows + 1)
            .Select(_ => NewItem())
            .ToList();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new ExportEmployeesRequest()));

        Assert.Equal(ErrorCode.Validation, ex.Code);
    }

    [Fact]
    public async Task 导出员工_空结果_应只有表头()
    {
        var (handler, employees, _) = CreateHandler();
        employees.Items = [];

        var result = await handler.HandleAsync(new ExportEmployeesRequest());

        using var workbook = new XLWorkbook(new MemoryStream(result.Content));
        var sheet = workbook.Worksheet("员工档案");
        Assert.True(sheet.Row(2).IsEmpty());
    }

    [Fact]
    public async Task 导出员工_性别未填且无部门岗位账号_应输出占位符()
    {
        var (handler, employees, _) = CreateHandler();
        employees.Items =
        [
            new EmployeeListItem
            {
                Id = Guid.NewGuid(),
                EmployeeNo = "E0002",
                Name = "李四",
                Gender = null,
                Phone = null,
                DepartmentId = null,
                DepartmentName = null,
                PositionId = null,
                PositionName = null,
                HireDate = new DateOnly(2026, 1, 2),
                ResignDate = null,
                Status = EmployeeStatus.Resigned,
                UserId = null,
                UserDisplayName = null,
                CreatedAt = BaseTime,
                CreatedBy = null,
            },
        ];

        var result = await handler.HandleAsync(new ExportEmployeesRequest());

        using var workbook = new XLWorkbook(new MemoryStream(result.Content));
        var sheet = workbook.Worksheet("员工档案");
        Assert.Equal("-", sheet.Cell(2, 3).GetString());
        Assert.Equal("-", sheet.Cell(2, 5).GetString());
        Assert.Equal("离职", sheet.Cell(2, 8).GetString());
        Assert.Equal("-", sheet.Cell(2, 9).GetString());
        Assert.Equal(string.Empty, sheet.Cell(2, 10).GetString());
    }
}
