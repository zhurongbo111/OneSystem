using App.Core.Entities;
using FluentValidation;

namespace App.Core.Features.Sales.UpdateSalesOrderSettlement;

/// <summary>
/// 销售单结算更新请求格式校验：settlementStatus ∈ {0, 1}
/// </summary>
public sealed class UpdateSalesOrderSettlementRequestValidator : AbstractValidator<UpdateSalesOrderSettlementRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateSalesOrderSettlementRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("销售单 id 不能为空");
        RuleFor(x => x.SettlementStatus)
            .Must(s => s is (int)OrderSettlementStatus.Unsettled or (int)OrderSettlementStatus.Settled)
            .WithMessage("结算状态取值非法");
    }
}
