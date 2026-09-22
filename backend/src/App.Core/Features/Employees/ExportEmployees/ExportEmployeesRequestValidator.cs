using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Employees.ExportEmployees;

/// <summary>
/// 员工列表导出请求格式校验：与列表同口径（分页参数仅供同形，越界仍按 40000 拒绝）
/// </summary>
public sealed class ExportEmployeesRequestValidator : AbstractValidator<ExportEmployeesRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public ExportEmployeesRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("页码必须大于等于 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        RuleFor(x => x.Status)
            .Must(status => status is null or EmployeeStatus.Active or EmployeeStatus.Resigned)
            .WithMessage("在职状态值无效");

        RuleFor(x => x.Keyword)
            .MaximumLength(EmployeeFieldConstraints.NameMaxLength)
            .WithMessage($"关键词长度不能超过 {EmployeeFieldConstraints.NameMaxLength}");
    }
}
