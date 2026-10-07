using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Attendances.GetAttendances;

/// <summary>
/// 考勤记录分页列表请求格式校验：只做数据格式检查，不访问仓储
/// </summary>
public sealed class GetAttendancesRequestValidator : AbstractValidator<GetAttendancesRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetAttendancesRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("页码必须大于等于 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        RuleFor(x => x.Type)
            .Must(type => type is null or (int)AttendanceType.Leave or (int)AttendanceType.Overtime)
            .WithMessage("考勤类型值无效");

        RuleFor(x => x.EndDate)
            .Must((request, endDate) => endDate is null || request.StartDate is null || endDate.Value >= request.StartDate.Value)
            .WithMessage("结束日不能早于起始日");
    }
}
