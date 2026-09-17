using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Settlements.GetUnsettledOrders;

/// <summary>
/// 可核销单据候选查询请求格式校验：只做数据格式检查
/// </summary>
public sealed class GetUnsettledOrdersRequestValidator : AbstractValidator<GetUnsettledOrdersRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetUnsettledOrdersRequestValidator()
    {
        RuleFor(x => x.PartnerId).NotEmpty().WithMessage("往来单位不能为空");

        RuleFor(x => x.Type)
            .Must(t => t is SettlementType.Receipt or SettlementType.Payment)
            .WithMessage("收付款类型取值非法");

        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("页码必须从 1 开始");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");
    }
}
