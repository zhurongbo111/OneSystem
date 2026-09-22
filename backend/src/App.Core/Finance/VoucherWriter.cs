using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Finance;

/// <summary>
/// 自动凭证写入编排（**静态辅助**，把仓储作为参数传入，不做状态持有）：
/// 校验期间（不存在 40159 / 已结账 40154）→ 取科目映射并校验科目合法（40157）→
/// <see cref="VoucherFactory"/> 构建 → 追加凭证（同一事务，与来源单据同生共死）。
/// 单据 Handler 只需在本用例既有事务块内调用一次。
/// </summary>
public static class VoucherWriter
{
    /// <summary>
    /// 在来源单据所在事务内追加一张自动凭证
    /// </summary>
    /// <param name="sourceType">来源类型</param>
    /// <param name="sourceId">来源单据 id</param>
    /// <param name="sourceNo">来源单据号</param>
    /// <param name="voucherDate">记账日期（决定归属期间）</param>
    /// <param name="totalAmount">单据含税总额</param>
    /// <param name="costAmount">成本结转金额（销售出库 / 销售退货；其他传 0）</param>
    /// <param name="settlementMethod">收付款方式（收付款单必传）</param>
    /// <param name="voucherRepository">凭证仓储</param>
    /// <param name="accountMappingRepository">科目映射仓储</param>
    /// <param name="accountingPeriodRepository">会计期间仓储</param>
    /// <param name="accountRepository">会计科目仓储</param>
    /// <param name="operatorId">操作人用户 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已追加的凭证（供调用方写审计摘要）</returns>
    public static async Task<Voucher> AppendAutoAsync(
        VoucherSourceType sourceType,
        Guid sourceId,
        string sourceNo,
        DateTimeOffset voucherDate,
        decimal totalAmount,
        decimal costAmount,
        SettlementMethod? settlementMethod,
        IVoucherRepository voucherRepository,
        IAccountMappingRepository accountMappingRepository,
        IAccountingPeriodRepository accountingPeriodRepository,
        IAccountRepository accountRepository,
        Guid? operatorId,
        CancellationToken cancellationToken = default)
    {
        var period = await EnsurePeriodOpenAsync(voucherDate, accountingPeriodRepository, cancellationToken);
        var accountsByKey = await ResolveAccountsAsync(accountMappingRepository, accountRepository, cancellationToken);
        var voucherNo = await voucherRepository.GenerateNoAsync(voucherDate, cancellationToken);

        var draft = VoucherFactory.Build(
            sourceType,
            sourceId,
            sourceNo,
            voucherDate,
            totalAmount,
            costAmount,
            settlementMethod,
            accountsByKey,
            period.Id,
            voucherNo,
            operatorId,
            DateTimeOffset.UtcNow);

        await voucherRepository.AddAsync(draft.Voucher, draft.Entries, cancellationToken);
        return draft.Voucher;
    }

    /// <summary>
    /// 来源单据作废时，在同一事务内作废其全部自动凭证（不生成红字凭证）
    /// </summary>
    /// <param name="sourceType">来源类型</param>
    /// <param name="sourceId">来源单据 id</param>
    /// <param name="voucherDate">来源单据日期（决定归属期间）</param>
    /// <param name="voucherRepository">凭证仓储</param>
    /// <param name="accountingPeriodRepository">会计期间仓储</param>
    /// <param name="operatorId">操作人用户 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task VoidAutoVouchersAsync(
        VoucherSourceType sourceType,
        Guid sourceId,
        DateTimeOffset voucherDate,
        IVoucherRepository voucherRepository,
        IAccountingPeriodRepository accountingPeriodRepository,
        Guid? operatorId,
        CancellationToken cancellationToken = default)
    {
        await EnsurePeriodOpenAsync(voucherDate, accountingPeriodRepository, cancellationToken);
        await voucherRepository.VoidBySourceAsync(sourceType, sourceId, operatorId, cancellationToken);
    }

    /// <summary>取记账日期所在期间；期间不存在 → 40159，已结账 → 40154</summary>
    private static async Task<AccountingPeriod> EnsurePeriodOpenAsync(
        DateTimeOffset voucherDate,
        IAccountingPeriodRepository accountingPeriodRepository,
        CancellationToken cancellationToken)
    {
        var period = await accountingPeriodRepository.GetByYearMonthAsync(
            voucherDate.Year,
            voucherDate.Month,
            cancellationToken)
            ?? throw new BusinessException(
                ErrorCode.PeriodNotOpened,
                $"记账日期 {voucherDate:yyyy-MM} 所在会计期间不存在，请先开账");

        if (period.Status == PeriodStatus.Closed)
        {
            throw new BusinessException(
                ErrorCode.PeriodClosed,
                $"会计期间 {period.Year}-{period.Month:00} 已结账，禁止记账");
        }

        return period;
    }

    /// <summary>
    /// 取科目映射字典（键 → 科目实体）；映射缺失的键不放入字典（工厂按需抛 40158），
    /// 映射指向的科目非末级或已停用 → 40157
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, Account>> ResolveAccountsAsync(
        IAccountMappingRepository accountMappingRepository,
        IAccountRepository accountRepository,
        CancellationToken cancellationToken)
    {
        var mappings = await accountMappingRepository.GetAllAsync(cancellationToken);
        if (mappings.Count == 0)
        {
            return new Dictionary<string, Account>(StringComparer.Ordinal);
        }

        var accounts = await accountRepository.GetAllAsync(cancellationToken);
        var accountsById = accounts.ToDictionary(a => a.Id);
        var nonLeafIds = accounts.Where(a => a.ParentId is not null).Select(a => a.ParentId!.Value).ToHashSet();

        var result = new Dictionary<string, Account>(StringComparer.Ordinal);
        foreach (var mapping in mappings)
        {
            if (!accountsById.TryGetValue(mapping.AccountId, out var account))
            {
                // 映射指向的科目已不存在：视同缺失映射，由工厂按需报 40158
                continue;
            }

            if (nonLeafIds.Contains(account.Id) || account.Status != AccountStatus.Enabled)
            {
                throw new BusinessException(
                    ErrorCode.VoucherAccountInvalid,
                    $"科目映射「{AccountMappingKeys.LabelOf(mapping.Key)}」指向的科目 {account.Code} {account.Name} 非末级或已停用");
            }

            result[mapping.Key] = account;
        }

        return result;
    }
}
