using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 采购订单仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL，销售订单 <c>ISalesOrderRepository</c> 同构）。
/// 订单是**计划数据**：不触碰库存与库存流水（specs/024-erp-order-flow design.md §1）；
/// 主表 + 明细的一次写操作由仓储自身持久化。
/// 出入库单关联回写（<c>AddFulfilledQuantityAsync</c>）与出入库单写入由 Handler 用 IUnitOfWork 包成同一事务。
/// </summary>
public interface IPurchaseOrderRepository
{
    /// <summary>
    /// 分页查询采购订单：订单号 / 供应商名称关键词 + 供应商 + 流转状态 + 下单日期闭区间，创建时间倒序；含作废订单。
    /// 同时返回每张订单的未收数量（Σ 明细未执行量 = Quantity − FulfilledQuantity），供列表展示在途量。
    /// </summary>
    /// <param name="keyword">订单号 / 供应商名称关键词，可空</param>
    /// <param name="partnerId">供应商 id，可空</param>
    /// <param name="flowStatus">订单流转状态，可空</param>
    /// <param name="start">起始下单日期（含），可空</param>
    /// <param name="end">结束下单日期（含），可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<(PurchaseOrder Order, int UnfulfilledQuantity)> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? partnerId,
        OrderFlowStatus? flowStatus,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询采购订单详情（主表实体 + 明细行实体，按明细 Id 还原插入顺序），不存在时 Order 为 null
    /// </summary>
    /// <param name="id">订单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(PurchaseOrder? Order, IReadOnlyList<PurchaseOrderItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询订单明细（供入库开单页带出未执行数量；未执行量 = Quantity − FulfilledQuantity，由调用方推导不落列）
    /// </summary>
    /// <param name="orderId">订单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(PurchaseOrder? Order, IReadOnlyList<PurchaseOrderItem> Items)> GetLinesAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询可关联的订单候选（指定供应商且流转状态为待收货 / 部分收货），创建时间倒序
    /// </summary>
    /// <param name="partnerId">供应商 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<PurchaseOrder>> GetPicksAsync(Guid partnerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增订单 + 明细并持久化（同一仓储内一次 SaveChanges）
    /// </summary>
    /// <param name="order">订单实体</param>
    /// <param name="items">明细行</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(PurchaseOrder order, IReadOnlyList<PurchaseOrderItem> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新订单主表 + 全量替换明细并持久化（仅「待收货」状态允许，状态判定在 Handler）
    /// </summary>
    /// <param name="order">订单实体（含最新主表字段）</param>
    /// <param name="items">新的明细行（整体替换，累计执行量恒为 0）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(PurchaseOrder order, IReadOnlyList<PurchaseOrderItem> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// 原子累加明细的累计执行量（关联出入库 +delta / 作废回退 −delta）并持久化
    /// </summary>
    /// <param name="orderItemId">订单明细行 id</param>
    /// <param name="delta">本次累加数量（正为执行、负为回退）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddFulfilledQuantityAsync(Guid orderItemId, int delta, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新订单流转状态（作废 / 关闭 / 部分收货 / 已完成）并持久化（同时写入 UpdatedBy / UpdatedAt 审计字段）
    /// </summary>
    /// <param name="id">订单 id</param>
    /// <param name="status">目标流转状态</param>
    /// <param name="operatorId">操作人 id（由 Handler 取 ICurrentUser 传入，可空）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateFlowStatusAsync(Guid id, OrderFlowStatus status, Guid? operatorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成订单号：前缀 + yyyyMMdd + 4 位序号（当天同前缀已有订单数 + 1）；
    /// 并发兜底由单号唯一索引承担，冲突重试由 Handler 处理（见 design.md §3.6）
    /// </summary>
    /// <param name="prefix">前缀（采购订单 PO / 销售订单 SO）</param>
    /// <param name="orderDate">下单日期（取 UTC 日期段）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<string> GenerateOrderNoAsync(string prefix, DateTimeOffset orderDate, CancellationToken cancellationToken = default);
}
