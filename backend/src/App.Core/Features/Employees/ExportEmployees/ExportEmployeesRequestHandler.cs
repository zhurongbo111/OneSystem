using App.Core.Abstractions;
using App.Core.Exports;

namespace App.Core.Features.Employees.ExportEmployees;

/// <summary>
/// 员工列表导出用例（erp-export）：复用员工列表的筛选与仓储查询取全量，
/// 导出列 = 列表列定义（去操作列）+ 创建人，输出单工作表 xlsx
/// </summary>
public sealed class ExportEmployeesRequestHandler : IRequestHandler<ExportEmployeesRequest, ExportResultDto>
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUserRepository _userRepository;
    private readonly IExcelExporter _excelExporter;

    /// <summary>
    /// 初始化员工列表导出用例处理器
    /// </summary>
    public ExportEmployeesRequestHandler(
        IEmployeeRepository employeeRepository,
        IUserRepository userRepository,
        IExcelExporter excelExporter)
    {
        _employeeRepository = employeeRepository;
        _userRepository = userRepository;
        _excelExporter = excelExporter;
    }

    /// <summary>
    /// 处理员工列表导出请求
    /// </summary>
    /// <param name="request">导出请求（筛选参数与列表一致）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ExportResultDto> HandleAsync(ExportEmployeesRequest request, CancellationToken cancellationToken = default)
    {
        // 按上限 + 1 取数：超出上限即明确报错，不静默截断（分页参数不参与，导出当前筛选全量）
        var items = await _employeeRepository.GetAllForExportAsync(
            request.Keyword,
            request.DepartmentId,
            request.PositionId,
            request.Status,
            ExportFieldConstraints.MaxRows + 1,
            cancellationToken);
        ExportGuards.EnsureWithinRowLimit(items.Count);

        var creatorNames = await _userRepository.GetDisplayNamesByIdsAsync(
            items.Where(i => i.CreatedBy is not null).Select(i => i.CreatedBy!.Value).Distinct().ToList(),
            cancellationToken);

        var rows = new List<IReadOnlyList<object?>>(items.Count);
        foreach (var item in items)
        {
            rows.Add(new object?[]
            {
                item.EmployeeNo,
                item.Name,
                ExportLabels.ToText(item.Gender),
                ExportLabels.OrDash(item.Phone),
                ExportLabels.OrDash(item.DepartmentName),
                ExportLabels.OrDash(item.PositionName),
                item.HireDate,
                ExportLabels.ToText(item.Status),
                ExportLabels.OrDash(item.UserDisplayName),
                item.CreatedBy is not null && creatorNames.TryGetValue(item.CreatedBy.Value, out var creator) ? creator : string.Empty,
            });
        }

        var sheet = new ExcelSheetModel
        {
            Name = ExportDomainNames.Employees,
            Columns =
            [
                new ExcelColumnModel { Header = "工号", ValueType = ExcelValueType.Text, Width = 18 },
                new ExcelColumnModel { Header = "姓名", ValueType = ExcelValueType.Text, Width = 14 },
                new ExcelColumnModel { Header = "性别", ValueType = ExcelValueType.Text, Width = 8 },
                new ExcelColumnModel { Header = "手机号", ValueType = ExcelValueType.Text, Width = 16 },
                new ExcelColumnModel { Header = "部门", ValueType = ExcelValueType.Text, Width = 18 },
                new ExcelColumnModel { Header = "岗位", ValueType = ExcelValueType.Text, Width = 16 },
                new ExcelColumnModel { Header = "入职日期", ValueType = ExcelValueType.Date, Width = 14 },
                new ExcelColumnModel { Header = "状态", ValueType = ExcelValueType.Text, Width = 10 },
                new ExcelColumnModel { Header = "关联账号", ValueType = ExcelValueType.Text, Width = 16 },
                new ExcelColumnModel { Header = "创建人", ValueType = ExcelValueType.Text, Width = 14 },
            ],
            Rows = rows,
        };

        var workbook = new ExcelWorkbookModel { Sheets = [sheet] };

        return new ExportResultDto
        {
            FileName = ExportFileNames.Build(ExportDomainNames.Employees, DateTimeOffset.UtcNow),
            Content = _excelExporter.Build(workbook),
        };
    }
}
