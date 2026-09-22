using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Vouchers.CreateVoucher;

/// <summary>
/// 手工凭证录入请求格式校验：只做数据格式检查；
/// 借贷平衡（40155）与科目末级启用（40157）在 Handler
/// </summary>
public sealed class CreateVoucherRequestValidator : AbstractValidator<CreateVoucherRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreateVoucherRequestValidator()
    {
        // 长度 / 条数 / 金额区间统一取自 VoucherFieldConstraints（禁止硬编码）
        RuleFor(x => x.VoucherDate)
            .Must(date => date != default)
            .WithMessage("记账日期不能为空");

        RuleFor(x => x.Summary)
            .NotEmpty().WithMessage("凭证摘要不能为空")
            .MaximumLength(VoucherFieldConstraints.SummaryMaxLength)
            .WithMessage($"凭证摘要长度不能超过 {VoucherFieldConstraints.SummaryMaxLength}");

        RuleFor(x => x.Items)
            .NotNull().WithMessage("凭证分录不能为空")
            .Must(items => items is { Count: > 0 })
            .WithMessage("凭证至少需要一条分录")
            .Must(items => items is null || items.Count <= VoucherFieldConstraints.EntriesMaxCount)
            .WithMessage($"凭证分录不能超过 {VoucherFieldConstraints.EntriesMaxCount} 条");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.AccountId)
                .NotEmpty().WithMessage("分录科目不能为空");

            item.RuleFor(i => i.Summary)
                .MaximumLength(VoucherFieldConstraints.EntrySummaryMaxLength)
                .WithMessage($"分录摘要长度不能超过 {VoucherFieldConstraints.EntrySummaryMaxLength}");

            item.RuleFor(i => i.Debit)
                .InclusiveBetween(VoucherFieldConstraints.PerEntryMinAmount, VoucherFieldConstraints.PerEntryMaxAmount)
                .WithMessage("借方金额超出允许范围");

            item.RuleFor(i => i.Credit)
                .InclusiveBetween(VoucherFieldConstraints.PerEntryMinAmount, VoucherFieldConstraints.PerEntryMaxAmount)
                .WithMessage("贷方金额超出允许范围");

            // 每行借方与贷方恰有一个大于 0
            item.RuleFor(i => i)
                .Must(i => (i.Debit > 0m) ^ (i.Credit > 0m))
                .WithMessage("每条分录的借方与贷方金额须恰有一个大于 0");
        });
    }
}
