namespace App.Core.Features.GeneralLedger;

/// <summary>
/// 凭证列表行出参模型（含 Status 供前端作废行置灰；枚举以整型输出）
/// </summary>
public sealed class VoucherListItemDto
{
    /// <summary>凭证 id</summary>
    public required string Id { get; init; }

    /// <summary>凭证号（记-YYYYMM-NNNN）</summary>
    public required string VoucherNo { get; init; }

    /// <summary>记账日期</summary>
    public required DateTimeOffset VoucherDate { get; init; }

    /// <summary>摘要</summary>
    public required string Summary { get; init; }

    /// <summary>来源类型（0 手工 / 1 采购入库 / 2 销售出库 / 3 采购退货 / 4 销售退货 / 5 收款 / 6 付款 / 7 成本结转）</summary>
    public required int SourceType { get; init; }

    /// <summary>来源单据 id，手工凭证为空</summary>
    public string? SourceId { get; init; }

    /// <summary>来源单据号快照，手工凭证为空</summary>
    public string? SourceNo { get; init; }

    /// <summary>借方合计</summary>
    public required decimal TotalDebit { get; init; }

    /// <summary>贷方合计</summary>
    public required decimal TotalCredit { get; init; }

    /// <summary>凭证状态（0 已作废 / 1 已过账）</summary>
    public required int Status { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// 凭证详情出参模型（主表 + 分录）
/// </summary>
public sealed class VoucherDetailDto
{
    /// <summary>凭证 id</summary>
    public required string Id { get; init; }

    /// <summary>凭证号</summary>
    public required string VoucherNo { get; init; }

    /// <summary>记账日期</summary>
    public required DateTimeOffset VoucherDate { get; init; }

    /// <summary>摘要</summary>
    public required string Summary { get; init; }

    /// <summary>来源类型</summary>
    public required int SourceType { get; init; }

    /// <summary>来源单据 id，手工凭证为空</summary>
    public string? SourceId { get; init; }

    /// <summary>来源单据号快照，手工凭证为空</summary>
    public string? SourceNo { get; init; }

    /// <summary>借方合计</summary>
    public required decimal TotalDebit { get; init; }

    /// <summary>贷方合计</summary>
    public required decimal TotalCredit { get; init; }

    /// <summary>凭证状态（0 已作废 / 1 已过账）</summary>
    public required int Status { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>更新时间</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>分录（按行号升序）</summary>
    public required IReadOnlyList<VoucherEntryDto> Items { get; init; }
}

/// <summary>凭证分录出参模型（科目编码 / 名称为分录快照）</summary>
public sealed class VoucherEntryDto
{
    /// <summary>分录 id</summary>
    public required string Id { get; init; }

    /// <summary>行号（1 起）</summary>
    public required int LineNo { get; init; }

    /// <summary>科目 id</summary>
    public required string AccountId { get; init; }

    /// <summary>科目编码快照</summary>
    public required string AccountCode { get; init; }

    /// <summary>科目名称快照</summary>
    public required string AccountName { get; init; }

    /// <summary>行摘要</summary>
    public string? Summary { get; init; }

    /// <summary>借方金额</summary>
    public required decimal Debit { get; init; }

    /// <summary>贷方金额</summary>
    public required decimal Credit { get; init; }
}
