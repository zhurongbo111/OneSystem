using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Finance;

namespace App.Core.Features.GeneralLedger;

/// <summary>
/// 总账域出参映射（集中一处，避免各用例重复拼装）
/// </summary>
internal static class GeneralLedgerDtoMapper
{
    /// <summary>会计期间实体 → 期间出参</summary>
    public static PeriodDto ToPeriodDto(AccountingPeriod period)
        => new()
        {
            Id = period.Id.ToString(),
            Year = period.Year,
            Month = period.Month,
            Status = (int)period.Status,
            ClosedAt = period.ClosedAt,
            ClosedBy = period.ClosedBy?.ToString(),
        };

    /// <summary>期间的展示文本（<c>YYYY-MM</c>，用于审计快照与人读摘要）</summary>
    public static string PeriodLabel(AccountingPeriod period) => $"{period.Year}-{period.Month:D2}";

    /// <summary>凭证实体 → 凭证列表行出参</summary>
    public static VoucherListItemDto ToVoucherListItemDto(Voucher voucher)
        => new()
        {
            Id = voucher.Id.ToString(),
            VoucherNo = voucher.VoucherNo,
            VoucherDate = voucher.VoucherDate,
            Summary = voucher.Summary,
            SourceType = (int)voucher.SourceType,
            SourceId = voucher.SourceId?.ToString(),
            SourceNo = voucher.SourceNo,
            TotalDebit = voucher.TotalDebit,
            TotalCredit = voucher.TotalCredit,
            Status = (int)voucher.Status,
            CreatedAt = voucher.CreatedAt,
        };

    /// <summary>凭证实体 + 分录 → 凭证详情出参</summary>
    public static VoucherDetailDto ToVoucherDetailDto(Voucher voucher, IReadOnlyList<VoucherEntry> entries)
        => new()
        {
            Id = voucher.Id.ToString(),
            VoucherNo = voucher.VoucherNo,
            VoucherDate = voucher.VoucherDate,
            Summary = voucher.Summary,
            SourceType = (int)voucher.SourceType,
            SourceId = voucher.SourceId?.ToString(),
            SourceNo = voucher.SourceNo,
            TotalDebit = voucher.TotalDebit,
            TotalCredit = voucher.TotalCredit,
            Status = (int)voucher.Status,
            CreatedAt = voucher.CreatedAt,
            UpdatedAt = voucher.UpdatedAt,
            Items = entries.Select(ToVoucherEntryDto).ToList(),
        };

    /// <summary>凭证分录实体 → 分录出参</summary>
    public static VoucherEntryDto ToVoucherEntryDto(VoucherEntry entry)
        => new()
        {
            Id = entry.Id.ToString(),
            LineNo = entry.LineNo,
            AccountId = entry.AccountId.ToString(),
            AccountCode = entry.AccountCode,
            AccountName = entry.AccountName,
            Summary = entry.Summary,
            Debit = entry.Debit,
            Credit = entry.Credit,
        };

    /// <summary>映射键 + 目标科目 → 科目映射出参（未配置科目时科目字段为空串）</summary>
    /// <param name="key">映射键</param>
    /// <param name="account">目标科目实体，未配置时为空</param>
    public static AccountMappingDto ToAccountMappingDto(string key, Account? account)
        => new()
        {
            Key = key,
            Label = AccountMappingKeys.LabelOf(key),
            AccountId = account?.Id.ToString() ?? string.Empty,
            AccountCode = account?.Code ?? string.Empty,
            AccountName = account?.Name ?? string.Empty,
        };

    /// <summary>科目余额读模型 → 科目余额表行出参</summary>
    public static AccountBalanceItemDto ToAccountBalanceItemDto(AccountBalanceItem item)
        => new()
        {
            AccountId = item.AccountId.ToString(),
            Code = item.Code,
            Name = item.Name,
            Category = (int)item.Category,
            Direction = (int)item.Direction,
            OpeningBalance = item.OpeningBalance,
            PeriodDebit = item.PeriodDebit,
            PeriodCredit = item.PeriodCredit,
            ClosingBalance = item.ClosingBalance,
        };

    /// <summary>资产负债表读模型 → 报表行出参</summary>
    public static BalanceSheetItemDto ToBalanceSheetItemDto(BalanceSheetItem item)
        => new()
        {
            AccountId = item.AccountId.ToString(),
            Code = item.Code,
            Name = item.Name,
            Amount = item.Amount,
        };

    /// <summary>利润表读模型 → 报表行出参</summary>
    public static IncomeStatementItemDto ToIncomeStatementItemDto(IncomeStatementItem item)
        => new()
        {
            AccountId = item.AccountId.ToString(),
            Code = item.Code,
            Name = item.Name,
            Amount = item.Amount,
        };
}
