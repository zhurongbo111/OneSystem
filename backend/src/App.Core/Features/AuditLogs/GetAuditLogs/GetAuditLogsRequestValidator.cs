using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.AuditLogs.GetAuditLogs;

/// <summary>
/// 操作日志查询请求格式校验：只做数据格式检查，不访问仓储
/// </summary>
public sealed class GetAuditLogsRequestValidator : AbstractValidator<GetAuditLogsRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetAuditLogsRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("页码必须大于等于 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        // 关键词上限对齐 ResourceNo 列长（超列长不可能命中）
        RuleFor(x => x.Keyword)
            .MaximumLength(AuditLogFieldConstraints.KeywordMaxLength)
            .WithMessage($"关键词长度不能超过 {AuditLogFieldConstraints.KeywordMaxLength}");

        RuleFor(x => x.Resource)
            .Must(value => value is null || Enum.IsDefined(typeof(AuditResource), value.Value))
            .WithMessage("资源类型不合法");

        RuleFor(x => x.Action)
            .Must(value => value is null || Enum.IsDefined(typeof(AuditAction), value.Value))
            .WithMessage("动作不合法");

        RuleFor(x => x)
            .Must(x => x.Start is null || x.End is null || x.Start <= x.End)
            .WithMessage("开始时间不能晚于结束时间");
    }
}
