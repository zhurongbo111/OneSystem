using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Settlements;

/// <summary>
/// 收付款单出参映射（实体 / 读模型 → DTO，禁止把实体暴露到 API）
/// </summary>
internal static class SettlementsDtoMapper
{
    /// <summary>
    /// 主表实体 + 核销明细转详情 DTO
    /// </summary>
    public static SettlementDetailDto ToSettlementDetailDto(SettlementDetail settlement, IReadOnlyList<SettlementItem> items)
        => new()
        {
            Id = settlement.Id.ToString(),
            SettlementNo = settlement.SettlementNo,
            Type = (int)settlement.Type,
            PartnerId = settlement.PartnerId.ToString(),
            PartnerName = settlement.PartnerName,
            SettlementDate = settlement.SettlementDate,
            TotalAmount = settlement.TotalAmount,
            Method = (int)settlement.Method,
            BankAccountId = settlement.BankAccountId,
            BankAccountName = settlement.BankAccountName,
            Status = (int)settlement.Status,
            Remark = settlement.Remark,
            CreatedBy = settlement.CreatedBy?.ToString(),
            CreatedAt = settlement.CreatedAt,
            Items = items.Select(i => new SettlementItemDto
            {
                Id = i.Id.ToString(),
                OrderType = (int)i.OrderType,
                OrderId = i.OrderId.ToString(),
                OrderNo = i.OrderNo,
                OrderDate = i.OrderDate,
                OrderTotalAmount = i.OrderTotalAmount,
                Amount = i.Amount,
            }).ToList(),
        };

    /// <summary>
    /// 主表实体转列表 DTO
    /// </summary>
    /// <param name="settlement">收付款单实体</param>
    /// <param name="orderAmount">本次核销金额（按被核销单据反查时传入；普通列表为 null）</param>
    /// <param name="orderTypes">该单核销明细的被核销单据类型集合（去重升序；无明细时为空集合）</param>
    public static SettlementListItemDto ToSettlementListItemDto(
        SettlementListItem settlement,
        decimal? orderAmount = null,
        IReadOnlyList<int>? orderTypes = null)
        => new()
        {
            Id = settlement.Id.ToString(),
            SettlementNo = settlement.SettlementNo,
            Type = (int)settlement.Type,
            PartnerId = settlement.PartnerId.ToString(),
            PartnerName = settlement.PartnerName,
            SettlementDate = settlement.SettlementDate,
            TotalAmount = settlement.TotalAmount,
            Method = (int)settlement.Method,
            BankAccountId = settlement.BankAccountId,
            BankAccountName = settlement.BankAccountName,
            Status = (int)settlement.Status,
            CreatedAt = settlement.CreatedAt,
            OrderAmount = orderAmount,
            OrderTypes = orderTypes ?? [],
        };

    /// <summary>
    /// 未结候选读模型转 DTO（未结金额在 Mapper 内推导）
    /// </summary>
    public static SettlementCandidateDto ToSettlementCandidateDto(SettlementCandidateItem candidate)
        => new()
        {
            OrderType = (int)candidate.OrderType,
            OrderId = candidate.OrderId.ToString(),
            OrderNo = candidate.OrderNo,
            OrderDate = candidate.OrderDate,
            TotalAmount = candidate.TotalAmount,
            SettledAmount = candidate.SettledAmount,
            UnsettledAmount = candidate.UnsettledAmount,
        };

    /// <summary>
    /// 往来台账读模型转 DTO
    /// </summary>
    public static ReconciliationListItemDto ToReconciliationListItemDto(ReconciliationItem item)
        => new()
        {
            PartnerId = item.PartnerId.ToString(),
            PartnerName = item.PartnerName,
            PartnerType = (int)item.PartnerType,
            ReceivableAmount = item.ReceivableAmount,
            PayableAmount = item.PayableAmount,
            UnsettledOrderCount = item.UnsettledOrderCount,
            PaymentTermDays = item.PaymentTermDays,
            EarliestDueDate = item.EarliestDueDate?.ToString("yyyy-MM-dd"),
            MaxOverdueDays = item.MaxOverdueDays,
            OverdueOrderCount = item.OverdueOrderCount,
        };
}
