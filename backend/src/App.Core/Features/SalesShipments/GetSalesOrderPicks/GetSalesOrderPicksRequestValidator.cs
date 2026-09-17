using FluentValidation;

namespace App.Core.Features.SalesShipments.GetSalesOrderPicks;

/// <summary>
/// 可关联销售订单候选查询请求格式校验：只做数据格式检查
/// </summary>
public sealed class GetSalesOrderPicksRequestValidator : AbstractValidator<GetSalesOrderPicksRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetSalesOrderPicksRequestValidator()
    {
        RuleFor(x => x.PartnerId).NotEmpty().WithMessage("客户不能为空");
    }
}
