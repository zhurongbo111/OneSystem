using FluentValidation;

namespace App.Core.Features.CashJournals.GetCashJournal;

/// <summary>
/// 资金日记账查询请求格式校验（specs/034-erp-cash/design.md §3.5）
/// </summary>
public sealed class GetCashJournalRequestValidator : AbstractValidator<GetCashJournalRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetCashJournalRequestValidator()
    {
        RuleFor(x => x.BankAccountId)
            .NotEmpty().WithMessage("资金账户不能为空");

        // 闭区间：结束日期不得早于起始日期（允许同一天）
        RuleFor(x => x.End)
            .GreaterThanOrEqualTo(x => x.Start)
            .WithMessage("结束日期不能早于起始日期");
    }
}
