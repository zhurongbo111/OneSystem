using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Leads.GetLeads;

/// <summary>
/// 线索分页查询请求格式校验：只做数据格式检查（分页边界、关键词长度、来源 / 状态取值）
/// </summary>
public sealed class GetLeadsRequestValidator : AbstractValidator<GetLeadsRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetLeadsRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("页码必须从 1 开始");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        // 长度统一取自 LeadFieldConstraints（禁止硬编码；等于列表模糊匹配列中的最大列长）
        RuleFor(x => x.Keyword).MaximumLength(LeadFieldConstraints.KeywordMaxLength).When(x => x.Keyword is not null);

        RuleFor(x => x.Source)
            .Must(s => s is null || Enum.IsDefined(s.Value))
            .WithMessage("线索来源取值非法");

        RuleFor(x => x.Status)
            .Must(s => s is null || Enum.IsDefined(s.Value))
            .WithMessage("线索状态取值非法");
    }
}
