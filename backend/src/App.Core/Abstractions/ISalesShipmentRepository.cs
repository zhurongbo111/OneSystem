using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 销售单仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL，与 <c>IPurchaseReceiptRepository</c> 同构）。
/// Repository 只做数据访问，不做业务判定；主表 + 明细的一次写操作由仓储自身持久化。
/// 跨仓储写（库存 N 行扣减 + 单据主表 + 明细）由 Handler 用 IUnitOfWork 包成同一事务。
/// </summary>
public interface ISalesShipmentRepository
{
    /// <summary>
    /// 分页查询销售单：单号 / 客户名称关键词 + 客户 + 日期闭区间 + 结算状态筛选，创建时间倒序；含作废单据
    /// </summary>
    /// <param name="keyword">单号 / 客户名称关键词，可空</param>
    /// <param name="partnerId">客户 id，可空</param>
    /// <param name="orderId">关联销售订单 id，可空（按订单查出库单）</param>
    /// <param name="start">起始业务日期（含），可空</param>
    /// <param name="end">结束业务日期（含），可空</param>
    /// <param name="settlementState">结算状态（0 未结 / 1 部分 / 2 结清，按 SettledAmount 与 TotalAmount 推导），可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<SalesShipment> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? partnerId,
        Guid? orderId,
        DateTimeOffset? start,
        DateTimeOffset? end,
        SettlementState? settlementState,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询销售单详情（主表实体 + 明细行实体，按明细 Id 还原插入顺序），不存在时 Order 为 null
    /// </summary>
    /// <param name="id">销售单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(SalesShipment? Order, IReadOnlyList<SalesShipmentItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增单据 + 明细并持久化（同一仓储内一次 SaveChanges）
    /// </summary>
    /// <param name="order">单据实体</param>
    /// <param name="items">明细行</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(SalesShipment order, IReadOnlyList<SalesShipmentItem> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// 原子累加已结算金额（核销 +delta / 作废回退 −delta）并持久化（同时写入 UpdatedBy / UpdatedAt 审计字段）；只允许收付款单核销 / 作废调用
    /// </summary>
    /// <param name="id">销售单 id</param>
    /// <param name="delta">本次累加金额（核销为正、作废回退为负）</param>
    /// <param name="operatorId">操作人 id（由 Handler 取 ICurrentUser 传入，可空）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddSettledAmountAsync(Guid id, decimal delta, Guid? operatorId, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// 按单据 id 集合批量查询明细行（erp-export 导出用，一次查询避免逐单 N+1），按明细 Id 升序（插入顺序）
    /// </summary>
    /// <param name="orderIds">销售单 id 集合（空集合返回空列表）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<SalesShipmentItem>> GetItemsByOrderIdsAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken = default);
}
