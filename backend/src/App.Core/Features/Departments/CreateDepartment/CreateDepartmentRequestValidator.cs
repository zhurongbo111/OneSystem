using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Departments.CreateDepartment;

/// <summary>
/// 新增部门请求格式校验：只做数据格式检查；
/// 编码 / 同级名称是否重复、上级是否存在等查库约束在 Handler 内
/// </summary>
public sealed class CreateDepartmentRequestValidator : AbstractValidator<CreateDepartmentRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreateDepartmentRequestValidator()
    {
        // 长度 / 区间统一取自 DepartmentFieldConstraints（与 EF 配置一致，禁止硬编码）
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("部门编码不能为空")
            .MaximumLength(DepartmentFieldConstraints.CodeMaxLength)
            .WithMessage($"部门编码长度不能超过 {DepartmentFieldConstraints.CodeMaxLength}");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("部门名称不能为空")
            .MaximumLength(DepartmentFieldConstraints.NameMaxLength)
            .WithMessage($"部门名称长度不能超过 {DepartmentFieldConstraints.NameMaxLength}");

        RuleFor(x => x.SortOrder)
            .InclusiveBetween(DepartmentFieldConstraints.SortOrderMinValue, DepartmentFieldConstraints.SortOrderMaxValue)
            .WithMessage($"排序必须在 {DepartmentFieldConstraints.SortOrderMinValue} 到 {DepartmentFieldConstraints.SortOrderMaxValue} 之间");

        RuleFor(x => x.Status)
            .Must(status => status is (int)DepartmentStatus.Enabled or (int)DepartmentStatus.Disabled)
            .WithMessage("部门状态值无效");

        RuleFor(x => x.Remark)
            .MaximumLength(DepartmentFieldConstraints.RemarkMaxLength)
            .WithMessage($"备注长度不能超过 {DepartmentFieldConstraints.RemarkMaxLength}");
    }
}
