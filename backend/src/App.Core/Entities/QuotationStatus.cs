namespace App.Core.Entities;

/// <summary>
/// 报价单状态（specs/037-erp-quotation design.md §0.1，取值与文案为该规格唯一事实源）。
/// 流转约束：编辑 / 作废仅限 <see cref="Draft"/>（否则 40166）；转单仅限 <see cref="Draft"/>（否则 40167）。
/// </summary>
public enum QuotationStatus
{
    /// <summary>草稿（可编辑 / 可作废 / 可转单）</summary>
    Draft = 0,

    /// <summary>已转订单（锁定，仅可查看）</summary>
    Converted = 1,

    /// <summary>已作废（终态，仅可查看）</summary>
    Voided = 2,
}
