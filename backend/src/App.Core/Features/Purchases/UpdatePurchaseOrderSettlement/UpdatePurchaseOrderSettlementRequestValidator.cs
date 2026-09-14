using App.Core.Entities;
using FluentValidation;

namespace App.Core.Features.Purchases.UpdatePurchaseOrderSettlement;

/// <summary>
/// 采购单结算更新请求格式校验：settlementStatus ∈ {0, 1}
/// </summary>
public sealed class UpdatePurchaseOrderSettlementRequestValidator : AbstractValidator<UpdatePurchaseOrderSettlementRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdatePurchaseOrderSettlementRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("采购单 id 不能为空");
        RuleFor(x => x.SettlementStatus)
            .Must(s => s is (int)OrderSettlementStatus.Unsettled or (int)OrderSettlementStatus.Settled)
            .WithMessage("结算状态取值非法");
    }
}
