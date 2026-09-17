using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.SalesReturns.UpdateSalesReturnSettlement;

/// <summary>
/// 销售退货单结算更新请求格式校验：settlementStatus ∈ {0, 1}
/// </summary>
public sealed class UpdateSalesReturnSettlementRequestValidator : AbstractValidator<UpdateSalesReturnSettlementRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateSalesReturnSettlementRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("销售退货单 id 不能为空");
        RuleFor(x => x.SettlementStatus)
            .Must(s => s is (int)OrderSettlementStatus.Unsettled or (int)OrderSettlementStatus.Settled)
            .WithMessage("结算状态取值非法");
    }
}
