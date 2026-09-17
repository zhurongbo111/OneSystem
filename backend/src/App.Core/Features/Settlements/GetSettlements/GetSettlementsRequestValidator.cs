using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Settlements.GetSettlements;

/// <summary>
/// 收付款单分页查询请求格式校验：只做数据格式检查
/// </summary>
public sealed class GetSettlementsRequestValidator : AbstractValidator<GetSettlementsRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetSettlementsRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("页码必须从 1 开始");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        // 长度统一取自 OrderFieldConstraints（禁止硬编码；与 SettlementNo 列长一致）
        RuleFor(x => x.Keyword).MaximumLength(OrderFieldConstraints.KeywordMaxLength).When(x => x.Keyword is not null);

        RuleFor(x => x.Type)
            .Must(t => t is null or SettlementType.Receipt or SettlementType.Payment)
            .WithMessage("收付款类型取值非法");

        RuleFor(x => x.Method)
            .Must(m => m is null or SettlementMethod.Cash or SettlementMethod.BankTransfer or SettlementMethod.Other)
            .WithMessage("收付款方式取值非法");

        // 日期范围闭区间：两者都传时 start <= end（跨字段校验用匿名类型组合）
        RuleFor(x => new { x.Start, x.End })
            .Must(v => v.Start is null || v.End is null || v.End >= v.Start)
            .WithMessage("结束日期不能早于开始日期");
    }
}
