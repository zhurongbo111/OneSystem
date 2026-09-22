using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Finance;

/// <summary>
/// 借贷平衡校验（**手工与自动凭证共用同一套**，见 specs/033-erp-general-ledger/design.md §0.1）：
/// 分录非空 + Σ 借方 = Σ 贷方，否则抛业务异常
/// </summary>
public static class VoucherBalanceValidator
{
    /// <summary>
    /// 校验分录集合满足借贷平衡，不平衡 / 为空时抛 <see cref="BusinessException"/>
    /// </summary>
    /// <param name="entries">待校验的分录集合</param>
    public static void EnsureBalanced(IReadOnlyList<VoucherEntry> entries)
    {
        if (entries.Count == 0)
        {
            throw new BusinessException(ErrorCode.VoucherNoEntries, "凭证至少需要一条分录");
        }

        var totalDebit = entries.Sum(e => e.Debit);
        var totalCredit = entries.Sum(e => e.Credit);
        if (totalDebit != totalCredit)
        {
            throw new BusinessException(
                ErrorCode.VoucherUnbalanced,
                $"凭证借贷不平衡（借方合计 {totalDebit:0.00}，贷方合计 {totalCredit:0.00}）");
        }
    }
}
