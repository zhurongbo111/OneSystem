using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Settlements.GetReconciliation;

/// <summary>
/// 往来对账台账查询请求格式校验：只做数据格式检查
/// </summary>
public sealed class GetReconciliationRequestValidator : AbstractValidator<GetReconciliationRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetReconciliationRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("页码必须从 1 开始");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        // 长度对齐往来名称列长（PartnerFieldConstraints.KeywordMaxLength，禁止硬编码）
        RuleFor(x => x.Keyword).MaximumLength(PartnerFieldConstraints.KeywordMaxLength).When(x => x.Keyword is not null);

        RuleFor(x => x.Type)
            .Must(t => t is null or PartnerType.Supplier or PartnerType.Customer or PartnerType.Both)
            .WithMessage("往来单位类型取值非法");
    }
}
