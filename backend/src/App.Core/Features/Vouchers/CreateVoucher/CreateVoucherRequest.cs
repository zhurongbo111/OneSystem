using App.Core.Abstractions;
using App.Core.Features.GeneralLedger;

namespace App.Core.Features.Vouchers.CreateVoucher;

/// <summary>
/// 手工凭证录入请求（借贷平衡在 Handler 校验）
/// </summary>
public sealed class CreateVoucherRequest : IRequest<VoucherDetailDto>
{
    /// <summary>记账日期（决定归属期间）</summary>
    public DateTimeOffset VoucherDate { get; init; }

    /// <summary>摘要</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>分录（1–200 条）</summary>
    public IReadOnlyList<CreateVoucherItem> Items { get; init; } = [];
}

/// <summary>手工凭证分录入参（借方与贷方恰有一个大于 0）</summary>
public sealed class CreateVoucherItem
{
    /// <summary>会计科目 id（须为末级且启用）</summary>
    public Guid AccountId { get; init; }

    /// <summary>行摘要，可空</summary>
    public string? Summary { get; init; }

    /// <summary>借方金额</summary>
    public decimal Debit { get; init; }

    /// <summary>贷方金额</summary>
    public decimal Credit { get; init; }
}
