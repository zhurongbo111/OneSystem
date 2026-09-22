using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Departments.UpdateDepartmentStatus;

/// <summary>
/// 部门停用 / 启用请求格式校验：只做数据格式检查；部门是否存在在 Handler 内
/// </summary>
public sealed class UpdateDepartmentStatusRequestValidator : AbstractValidator<UpdateDepartmentStatusRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateDepartmentStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(status => status is (int)DepartmentStatus.Enabled or (int)DepartmentStatus.Disabled)
            .WithMessage("部门状态值无效");
    }
}
