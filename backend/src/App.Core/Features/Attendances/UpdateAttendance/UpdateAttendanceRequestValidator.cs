using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Attendances.UpdateAttendance;

/// <summary>
/// 编辑考勤登记请求格式校验：只做数据格式检查；
/// 存在性、员工在职、同类型区间重叠等查库约束在 Handler 内
/// </summary>
public sealed class UpdateAttendanceRequestValidator : AbstractValidator<UpdateAttendanceRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateAttendanceRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("考勤记录不能为空");

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
