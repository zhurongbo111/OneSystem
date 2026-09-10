using App.Core.Entities;
using FluentValidation;

namespace App.Core.Features.LoginLogs.GetLoginLogs;

/// <summary>
/// 登录日志查询请求格式校验：只做数据格式检查，不访问仓储
/// </summary>
public sealed class GetLoginLogsRequestValidator : AbstractValidator<GetLoginLogsRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetLoginLogsRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("页码必须大于等于 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        // 登录名快照列长度对齐 Users.Username
        RuleFor(x => x.Username)
            .MaximumLength(UserFieldConstraints.UsernameMaxLength)
            .WithMessage($"登录名关键词长度不能超过 {UserFieldConstraints.UsernameMaxLength}");

        RuleFor(x => x)
            .Must(x => x.StartTime is null || x.EndTime is null || x.StartTime <= x.EndTime)
            .WithMessage("开始时间不能晚于结束时间");
    }
}
