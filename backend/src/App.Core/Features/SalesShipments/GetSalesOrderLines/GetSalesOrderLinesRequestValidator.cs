using FluentValidation;

namespace App.Core.Features.SalesShipments.GetSalesOrderLines;

/// <summary>
/// 关联订单明细查询请求格式校验：只做数据格式检查
/// </summary>
public sealed class GetSalesOrderLinesRequestValidator : AbstractValidator<GetSalesOrderLinesRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetSalesOrderLinesRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("订单 id 不能为空");
    }
}
