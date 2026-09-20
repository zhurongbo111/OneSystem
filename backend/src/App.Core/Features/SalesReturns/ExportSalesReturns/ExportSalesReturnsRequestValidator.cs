using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.SalesReturns.ExportSalesReturns;

/// <summary>
/// 销售退货单导出请求格式校验：与列表查询规则完全一致（只做数据格式检查）
/// </summary>
public sealed class ExportSalesReturnsRequestValidator : AbstractValidator<ExportSalesReturnsRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public ExportSalesReturnsRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("页码必须从 1 开始");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        // 长度统一取自 OrderFieldConstraints（禁止硬编码；与 ReturnNo 列长一致）
        RuleFor(x => x.Keyword).MaximumLength(OrderFieldConstraints.KeywordMaxLength).When(x => x.Keyword is not null);

        RuleFor(x => x.SettlementState)
            .Must(s => s is null or SettlementState.Unsettled or SettlementState.PartiallySettled or SettlementState.Settled)
            .WithMessage("结算状态取值非法");

        // 日期范围闭区间：两者都传时 start <= end
        RuleFor(x => new { x.Start, x.End })
            .Must(v => v.Start is null || v.End is null || v.End >= v.Start)
            .WithMessage("结束日期不能早于开始日期");
    }
}
