using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Finance;

/// <summary>
/// 自动凭证工厂（**无状态技术组件**，不注入仓储）：
/// 入参「来源类型 + 金额 + 日期 + 单据号 + 科目映射」→ 产出 <see cref="Voucher"/> 与分录集合
/// （模板见 specs/033-erp-general-ledger/design.md §0.2）。
/// 映射缺失抛 <see cref="ErrorCode.AccountMappingMissing"/>；借贷平衡由 <see cref="VoucherBalanceValidator"/> 统一校验。
/// </summary>
public static class VoucherFactory
{
    /// <summary>
    /// 按来源类型构建自动凭证（分录模板见 design.md §0.2）
    /// </summary>
    /// <param name="sourceType">来源类型（<see cref="VoucherSourceType.Manual"/> 不支持，手工凭证自行构建）</param>
    /// <param name="sourceId">来源单据 id</param>
    /// <param name="sourceNo">来源单据号</param>
    /// <param name="voucherDate">记账日期</param>
    /// <param name="totalAmount">单据含税总额</param>
    /// <param name="costAmount">成本结转金额（销售出库 / 销售退货；其他来源传 0）</param>
    /// <param name="settlementMethod">收付款方式（收付款单必传，用于选现金 / 银行科目）</param>
    /// <param name="accountsByKey">科目映射字典（键 → 科目实体）</param>
    /// <param name="periodId">归属会计期间 id</param>
    /// <param name="voucherNo">凭证号</param>
    /// <param name="operatorId">操作人用户 id</param>
    /// <param name="now">当前时间</param>
    public static VoucherDraft Build(
        VoucherSourceType sourceType,
        Guid sourceId,
        string sourceNo,
        DateTimeOffset voucherDate,
        decimal totalAmount,
        decimal costAmount,
        SettlementMethod? settlementMethod,
        IReadOnlyDictionary<string, Account> accountsByKey,
        Guid periodId,
        string voucherNo,
        Guid? operatorId,
        DateTimeOffset now)
    {
        var lines = new List<DraftLine>();

        switch (sourceType)
        {
            case VoucherSourceType.PurchaseInbound:
                // 借 存货 / 贷 应付账款（含税总额）
                lines.Add(new DraftLine(AccountMappingKeys.Inventory, AccountMappingKeys.Payable, totalAmount));
                break;

            case VoucherSourceType.SalesOutbound:
                // 借 应收账款 / 贷 主营业务收入；成本结转并入同一凭证：借 主营业务成本 / 贷 存货
                lines.Add(new DraftLine(AccountMappingKeys.Receivable, AccountMappingKeys.Revenue, totalAmount));
                AddCostCarry(lines, costAmount, isReversal: false);
                break;

            case VoucherSourceType.PurchaseReturn:
                // 借 应付账款 / 贷 存货（红字冲减）
                lines.Add(new DraftLine(AccountMappingKeys.Payable, AccountMappingKeys.Inventory, totalAmount));
                break;

            case VoucherSourceType.SalesReturn:
                // 借 主营业务收入 / 贷 应收账款；成本转回：借 存货 / 贷 主营业务成本
                lines.Add(new DraftLine(AccountMappingKeys.Revenue, AccountMappingKeys.Receivable, totalAmount));
                AddCostCarry(lines, costAmount, isReversal: true);
                break;

            case VoucherSourceType.Receipt:
                // 借 现金 / 银行存款（按结算方式）/ 贷 应收账款
                lines.Add(new DraftLine(ResolveCashKey(settlementMethod), AccountMappingKeys.Receivable, totalAmount));
                break;

            case VoucherSourceType.Payment:
                // 借 应付账款 / 贷 现金 / 银行存款（按结算方式）
                lines.Add(new DraftLine(AccountMappingKeys.Payable, ResolveCashKey(settlementMethod), totalAmount));
                break;

            default:
                throw new BusinessException(ErrorCode.VoucherAccountInvalid, "该来源类型不支持自动凭证");
        }

        // 去掉金额为 0 的行（如无成本结转的销售出库）
        lines = lines.Where(l => l.Amount != 0m).ToList();
        if (lines.Count == 0)
        {
            throw new BusinessException(ErrorCode.VoucherNoEntries, "凭证至少需要一条分录");
        }

        var entries = new List<VoucherEntry>(lines.Count * 2);
        var lineNo = 1;
        foreach (var line in lines)
        {
            var debitAccount = ResolveAccount(accountsByKey, line.DebitKey);
            var creditAccount = ResolveAccount(accountsByKey, line.CreditKey);
            entries.Add(NewEntry(lineNo++, debitAccount, line.Amount, 0m));
            entries.Add(NewEntry(lineNo++, creditAccount, 0m, line.Amount));
        }

        VoucherBalanceValidator.EnsureBalanced(entries);

        var voucher = new Voucher
        {
            Id = Guid.NewGuid(),
            VoucherNo = voucherNo,
            VoucherDate = voucherDate,
            PeriodId = periodId,
            Summary = $"{SourceTypeLabel(sourceType)} {sourceNo}",
            SourceType = sourceType,
            SourceId = sourceId,
            SourceNo = sourceNo,
            TotalDebit = entries.Sum(e => e.Debit),
            TotalCredit = entries.Sum(e => e.Credit),
            Status = VoucherStatus.Posted,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        foreach (var entry in entries)
        {
            entry.VoucherId = voucher.Id;
        }

        return new VoucherDraft(voucher, entries);
    }

    /// <summary>取来源类型的中文文案（摘要用）</summary>
    /// <param name="sourceType">来源类型</param>
    public static string SourceTypeLabel(VoucherSourceType sourceType) => sourceType switch
    {
        VoucherSourceType.Manual => "手工凭证",
        VoucherSourceType.PurchaseInbound => "采购入库单",
        VoucherSourceType.SalesOutbound => "销售出库单",
        VoucherSourceType.PurchaseReturn => "采购退货单",
        VoucherSourceType.SalesReturn => "销售退货单",
        VoucherSourceType.Receipt => "收款单",
        VoucherSourceType.Payment => "付款单",
        VoucherSourceType.CostCarry => "成本结转",
        _ => string.Empty,
    };

    /// <summary>收付款选科目：现金 → 现金科目；银行转账 / 其他 → 银行科目（design.md §0.2）</summary>
    private static string ResolveCashKey(SettlementMethod? method)
        => method == SettlementMethod.Cash ? AccountMappingKeys.Cash : AccountMappingKeys.Bank;

    /// <summary>成本结转分录：销售出库（借成本 / 贷存货）或销售退货成本转回（借存货 / 贷成本）</summary>
    private static void AddCostCarry(List<DraftLine> lines, decimal costAmount, bool isReversal)
        => lines.Add(isReversal
            ? new DraftLine(AccountMappingKeys.Inventory, AccountMappingKeys.Cost, costAmount)
            : new DraftLine(AccountMappingKeys.Cost, AccountMappingKeys.Inventory, costAmount));

    /// <summary>按映射键取科目；缺失映射拒绝生成（40158）</summary>
    private static Account ResolveAccount(IReadOnlyDictionary<string, Account> accountsByKey, string key)
    {
        if (!accountsByKey.TryGetValue(key, out var account))
        {
            throw new BusinessException(
                ErrorCode.AccountMappingMissing,
                $"缺少科目映射：{AccountMappingKeys.LabelOf(key)}");
        }

        return account;
    }

    /// <summary>构建一条分录（科目快照 + 借贷金额）</summary>
    private static VoucherEntry NewEntry(int lineNo, Account account, decimal debit, decimal credit)
        => new()
        {
            Id = SequentialGuidGenerator.NewSequential(),
            LineNo = lineNo,
            AccountId = account.Id,
            AccountCode = account.Code,
            AccountName = account.Name,
            Debit = debit,
            Credit = credit,
        };

    /// <summary>凭证模板分录（借方映射键 / 贷方映射键 / 金额）</summary>
    private sealed record DraftLine(string DebitKey, string CreditKey, decimal Amount);
}

/// <summary>自动凭证构建结果（凭证主表 + 分录集合）</summary>
/// <param name="Voucher">凭证主表（已填 <c>VoucherNo</c> / <c>PeriodId</c> / 借贷合计）</param>
/// <param name="Entries">分录集合（借贷平衡、行号连续）</param>
public sealed record VoucherDraft(Voucher Voucher, IReadOnlyList<VoucherEntry> Entries);
