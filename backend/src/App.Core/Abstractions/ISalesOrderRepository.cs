using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 销售单仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL，与 <c>IPurchaseOrderRepository</c> 同构）。
/// Repository 只做数据访问，不做业务判定；主表 + 明细的一次写操作由仓储自身持久化。
/// 跨仓储写（库存 N 行扣减 + 单据主表 + 明细）由 Handler 用 IUnitOfWork 包成同一事务。
/// </summary>
public interface ISalesOrderRepository
{
    /// <summary>
    /// 分页查询销售单：单号 / 客户名称关键词 + 客户 + 日期闭区间 + 结算状态筛选，创建时间倒序；含作废单据
    /// </summary>
    /// <param name="keyword">单号 / 客户名称关键词，可空</param>
    /// <param name="partnerId">客户 id，可空</param>
    /// <param name="start">起始业务日期（含），可空</param>
    /// <param name="end">结束业务日期（含），可空</param>
    /// <param name="settlement">结算状态，可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<SalesOrderListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? partnerId,
        DateTimeOffset? start,
        DateTimeOffset? end,
        OrderSettlementStatus? settlement,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询销售单详情（主表 + 明细行，按明细插入顺序），不存在返回 null
    /// </summary>
    /// <param name="id">销售单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<SalesOrderDetail?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增单据 + 明细并持久化（同一仓储内一次 SaveChanges）
    /// </summary>
    /// <param name="order">单据实体</param>
    /// <param name="items">明细行</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(SalesOrder order, IReadOnlyList<SalesOrderItem> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新结算状态并持久化（同时写入 UpdatedBy / UpdatedAt 审计字段）
    /// </summary>
    /// <param name="id">销售单 id</param>
    /// <param name="settlement">目标结算状态</param>
    /// <param name="operatorId">操作人 id（由 Handler 取 ICurrentUser 传入，可空）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateSettlementAsync(Guid id, OrderSettlementStatus settlement, Guid? operatorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新单据状态（作废）并持久化（同时写入 UpdatedBy / UpdatedAt 审计字段）
    /// </summary>
    /// <param name="id">销售单 id</param>
    /// <param name="status">目标状态</param>
    /// <param name="operatorId">操作人 id（由 Handler 取 ICurrentUser 传入，可空）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateStatusAsync(Guid id, OrderStatus status, Guid? operatorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成单号：前缀 + yyyyMMdd + 4 位序号（当天同前缀已有单号数 + 1）；
    /// 并发兜底由单号唯一索引承担，冲突重试由 Handler 处理（见 design.md §3.6）
    /// </summary>
    /// <param name="prefix">前缀（销售 SO）</param>
    /// <param name="orderDate">业务日期（取 UTC 日期段）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<string> GenerateOrderNoAsync(string prefix, DateTimeOffset orderDate, CancellationToken cancellationToken = default);
}
