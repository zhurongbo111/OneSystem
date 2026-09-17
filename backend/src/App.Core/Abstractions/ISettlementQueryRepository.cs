using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 结算跨表只读查询仓储接口（实现见 App.Infrastructure）：未结单据候选 + 往来对账台账。
/// 跨四张单据表的只读聚合不属于任何单一单据仓储，故按「查询职责」独立成接口；写侧仍由各单据仓储负责。
/// </summary>
public interface ISettlementQueryRepository
{
    /// <summary>
    /// 查询某往来单位在指定方向下的可核销单据（未结金额 &gt; 0 且未作废），按业务日期倒序。
    /// 收款 → 销售出库单 + 采购退货单；付款 → 采购入库单 + 销售退货单；四表查询后在内存合并分页。
    /// </summary>
    /// <param name="partnerId">往来单位 id</param>
    /// <param name="type">收付款类型（决定可核销的单据类型集合）</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<SettlementCandidateItem> Items, int Total)> GetUnsettledAsync(
        Guid partnerId,
        SettlementType type,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询往来台账：按往来单位聚合应收 / 应付余额与未结单据数（keyword 匹配往来名称 / 编码），按往来名称排序。
    /// </summary>
    /// <param name="keyword">往来名称 / 编码关键词，可空</param>
    /// <param name="type">往来单位类型筛选，可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<ReconciliationItem> Items, int Total)> GetReconciliationAsync(
        string? keyword,
        PartnerType? type,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
