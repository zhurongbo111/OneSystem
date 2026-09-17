using FluentValidation;

namespace App.Core.Features.PurchaseReceipts.GetPurchaseOrderPicks;

/// <summary>
/// 可关联采购订单候选查询请求格式校验：只做数据格式检查
/// </summary>
public sealed class GetPurchaseOrderPicksRequestValidator : AbstractValidator<GetPurchaseOrderPicksRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetPurchaseOrderPicksRequestValidator()
    {
        RuleFor(x => x.PartnerId).NotEmpty().WithMessage("供应商不能为空");
    }
}
