using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 报价单列表行读模型（仓储出参契约 ↔ Mapper 传递，不暴露到 API）：
/// 主表实体 + 明细行数（`QuotationItems` 聚合，实体上没有该字段，故独立成读模型，后端规则 §4.3）。
/// </summary>
public sealed record QuotationListItem
{
    /// <summary>报价单主表实体</summary>
    public required Quotation Quotation { get; init; }

    /// <summary>明细行数</summary>
    public required int ItemCount { get; init; }
}
