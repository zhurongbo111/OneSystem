using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 发票仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// Repository 只做数据访问，不做业务判定；主表 + 关联明细的一次写操作由仓储自身持久化
/// （specs/032-erp-invoice/design.md §3.1）。
/// </summary>
public interface IInvoiceRepository
{
    /// <summary>
    /// 分页查询发票：发票号 / 往来名称 / 关联单据号关键词 + 类型 + 往来 + 开票日期闭区间，创建时间倒序；含作废发票。
    /// 关联单据号检索走明细 EXISTS 子查询；返回读模型（含关联单据号拼接）
    /// </summary>
    /// <param name="keyword">发票号 / 往来名称 / 关联单据号关键词，可空</param>
    /// <param name="type">发票类型（0 进项 / 1 销项），可空</param>
    /// <param name="partnerId">往来单位 id，可空</param>
    /// <param name="start">起始开票日期（含），可空</param>
    /// <param name="end">结束开票日期（含），可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<InvoiceListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        InvoiceType? type,
        Guid? partnerId,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询发票详情（主表实体 + 关联明细，按明细 Id 还原插入顺序），不存在时 Invoice 为 null
    /// </summary>
    /// <param name="id">发票 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(Invoice? Invoice, IReadOnlyList<InvoiceItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发票号是否已存在（全局唯一，忽略大小写判定由查询层归一化）
    /// </summary>
    /// <param name="invoiceNo">发票号码</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsByInvoiceNoAsync(string invoiceNo, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增发票 + 关联明细并持久化（同一仓储内一次 SaveChanges）；
    /// 发票号唯一约束冲突时抛 <c>BusinessException(40132)</c>（并发兜底）
    /// </summary>
    /// <param name="invoice">发票实体</param>
    /// <param name="items">关联明细行</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(Invoice invoice, IReadOnlyList<InvoiceItem> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新发票状态（作废）并持久化（同时写入 UpdatedBy / UpdatedAt 审计字段）
    /// </summary>
    /// <param name="id">发票 id</param>
    /// <param name="status">目标状态</param>
    /// <param name="operatorId">操作人 id（由 Handler 取 ICurrentUser 传入，可空）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateStatusAsync(Guid id, OrderStatus status, Guid? operatorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按发票 id 集合批量查询关联明细行（导出与列表聚合共用，一次查询避免逐单 N+1），按明细 Id 升序（插入顺序）
    /// </summary>
    /// <param name="invoiceIds">发票 id 集合（空集合返回空列表）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<InvoiceItem>> GetItemsByInvoiceIdsAsync(IReadOnlyCollection<Guid> invoiceIds, CancellationToken cancellationToken = default);
}