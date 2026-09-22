using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Employees.GetEmployees;

/// <summary>
/// 员工分页列表请求格式校验：只做数据格式检查，不访问仓储
/// </summary>
public sealed class GetEmployeesRequestValidator : AbstractValidator<GetEmployeesRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetEmployeesRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("页码必须大于等于 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        RuleFor(x => x.Status)
            .Must(status => status is null or (int)EmployeeStatus.Active or (int)EmployeeStatus.Resigned)
            .WithMessage("在职状态值无效");

        // 关键词匹配工号（20）/ 姓名（50），上限取二者较大者（姓名列），超列长不可能命中
        RuleFor(x => x.Keyword)
            .MaximumLength(EmployeeFieldConstraints.NameMaxLength)
            .WithMessage($"关键词长度不能超过 {EmployeeFieldConstraints.NameMaxLength}");
    }
}
