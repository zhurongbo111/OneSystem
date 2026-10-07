using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Attendances.CreateAttendance;

/// <summary>
/// 新增考勤登记请求格式校验：只做数据格式检查；
/// 员工存在性与在职判定、同类型区间重叠等查库约束在 Handler 内
/// </summary>
public sealed class CreateAttendanceRequestValidator : AbstractValidator<CreateAttendanceRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreateAttendanceRequestValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("员工不能为空");

        RuleFor(x => x.Type)
            .Must(type => type is (int)AttendanceType.Leave or (int)AttendanceType.Overtime)
            .WithMessage("考勤类型值无效");

        RuleFor(x => x.StartDate)
            .Must(startDate => startDate != default).WithMessage("起始日不能为空");

        RuleFor(x => x.EndDate)
            .Must(endDate => endDate != default).WithMessage("结束日不能为空")
            .Must((request, endDate) => endDate >= request.StartDate).WithMessage("结束日不能早于起始日");

        RuleFor(x => x.Remark)
            .MaximumLength(AttendanceFieldConstraints.RemarkMaxLength)
            .WithMessage($"事由长度不能超过 {AttendanceFieldConstraints.RemarkMaxLength}");
    }
}
