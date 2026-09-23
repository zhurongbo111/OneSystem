using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 报价单仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// 报价单是**意向数据**：不触碰库存、库存流水与收付款（specs/037-erp-quotation design.md §1）；
/// 主表 + 明细的一次写操作由仓储自身持久化；转单（建销售订单 + 回写报价单）由 Handler 用 IUnitOfWork 包成同一事务。
/// </summary>
public interface IQuotationRepository
{
    /// <summary>
    /// 分页查询报价单：单号 / 客户名称关键词 + 状态 + 报价日期闭区间，创建时间倒序；含已转订单 / 已作废单。
    /// 同时返回每张报价单的明细行数（`QuotationItems` 聚合），供列表展示。
    /// </summary>
    /// <param name="keyword">报价单号 / 客户名称关键词，可空</param>
    /// <param name="status">报价单状态，可空</param>
    /// <param name="start">起始报价日期（含），可空</param>
    /// <param name="end">结束报价日期（含），可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<QuotationListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        QuotationStatus? status,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询报价单详情（主表实体 + 明细行实体，按明细 Id 还原插入顺序），不存在时 Quotation 为 null
    /// </summary>
    /// <param name="id">报价单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(Quotation? Quotation, IReadOnlyList<QuotationItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增报价单 + 明细并持久化（同一仓储内一次 SaveChanges）
    /// </summary>
    /// <param name="quotation">报价单实体</param>
    /// <param name="items">明细行</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(Quotation quotation, IReadOnlyList<QuotationItem> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新报价单主表 + 全量替换明细并持久化（仅草稿允许，状态判定在 Handler）
    /// </summary>
    /// <param name="quotation">报价单实体（含最新主表字段）</param>
    /// <param name="items">新的明细行（整体替换）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(Quotation quotation, IReadOnlyList<QuotationItem> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新报价单状态并持久化（作废 / 转单回写；同时写入 UpdatedBy / UpdatedAt 审计字段）。
    /// 作废时 <paramref name="convertedOrderId"/> / <paramref name="convertedOrderNo"/> 传 null（原本即为空）。
    /// </summary>
    /// <param name="id">报价单 id</param>
    /// <param name="status">目标状态</param>
    /// <param name="convertedOrderId">转出的销售订单 id，可空</param>
    /// <param name="convertedOrderNo">转出的销售订单号快照，可空</param>
    /// <param name="operatorId">操作人 id（由 Handler 取 ICurrentUser 传入，可空）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateStatusAsync(
        Guid id,
        QuotationStatus status,
        Guid? convertedOrderId,
        string? convertedOrderNo,
        Guid? operatorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成报价单号：前缀 + yyyyMMdd + 4 位序号（当天同前缀已有报价单数 + 1）；
    /// 并发兜底由单号唯一索引承担，冲突重试由 Handler 处理（见 specs/015-erp-purchase design.md §3.6）
    /// </summary>
    /// <param name="prefix">前缀（报价单 QT）</param>
    /// <param name="quotationDate">报价日期（取 UTC 日期段）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<string> GenerateNoAsync(string prefix, DateTimeOffset quotationDate, CancellationToken cancellationToken = default);
}
