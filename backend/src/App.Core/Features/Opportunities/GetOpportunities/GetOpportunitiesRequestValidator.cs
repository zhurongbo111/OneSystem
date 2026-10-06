using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Opportunities.GetOpportunities;

/// <summary>
/// 商机分页查询请求格式校验：只做数据格式检查（分页边界、关键词长度、阶段取值）
/// </summary>
public sealed class GetOpportunitiesRequestValidator : AbstractValidator<GetOpportunitiesRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetOpportunitiesRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("页码必须从 1 开始");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        // 长度统一取自 OpportunityFieldConstraints（禁止硬编码；等于列表模糊匹配列中的最大列长）
        RuleFor(x => x.Keyword).MaximumLength(OpportunityFieldConstraints.KeywordMaxLength).When(x => x.Keyword is not null);

        RuleFor(x => x.Stage)
            .Must(s => s is null || Enum.IsDefined(s.Value))
            .WithMessage("商机阶段取值非法");
    }
}
