using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.PurchaseReturns.UpdatePurchaseReturnSettlement;

/// <summary>
/// 采购退货单结算更新请求格式校验：settlementStatus ∈ {0, 1}
/// </summary>
public sealed class UpdatePurchaseReturnSettlementRequestValidator : AbstractValidator<UpdatePurchaseReturnSettlementRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdatePurchaseReturnSettlementRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("采购退货单 id 不能为空");
        RuleFor(x => x.SettlementStatus)
            .Must(s => s is (int)OrderSettlementStatus.Unsettled or (int)OrderSettlementStatus.Settled)
            .WithMessage("结算状态取值非法");
    }
}
