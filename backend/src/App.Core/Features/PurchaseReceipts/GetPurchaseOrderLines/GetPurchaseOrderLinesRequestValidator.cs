using FluentValidation;

namespace App.Core.Features.PurchaseReceipts.GetPurchaseOrderLines;

/// <summary>
/// 关联订单明细查询请求格式校验：只做数据格式检查
/// </summary>
public sealed class GetPurchaseOrderLinesRequestValidator : AbstractValidator<GetPurchaseOrderLinesRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetPurchaseOrderLinesRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("订单 id 不能为空");
    }
}
