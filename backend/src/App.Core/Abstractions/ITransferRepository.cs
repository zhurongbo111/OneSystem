using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 调拨单仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL，specs/039-erp-transfer）。
/// Repository 只做数据访问，不做业务判定；主表 + 明细的一次写操作由仓储自身持久化。
/// 跨仓储写（单据 + 明细 + 库存 N 行 + 流水 2N 条）由 Handler 用 IUnitOfWork 包成同一事务。
/// </summary>
public interface ITransferRepository
{
    /// <summary>
    /// 分页查询调拨单：单号关键词 + 转出 / 转入仓 + 日期闭区间筛选，创建时间倒序；含作废单据
    /// </summary>
    /// <param name="keyword">单号关键词，可空（匹配 TransferNo，忽略大小写）</param>
    /// <param name="fromWarehouseId">转出仓 id，可空（不传 = 全部仓）</param>
    /// <param name="toWarehouseId">转入仓 id，可空（不传 = 全部仓）</param>
    /// <param name="start">起始业务日期（含），可空</param>
    /// <param name="end">结束业务日期（含），可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<Transfer> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? fromWarehouseId,
        Guid? toWarehouseId,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询调拨单详情（主表实体 + 明细行实体，按明细 Id 还原插入顺序），不存在时 Transfer 为 null
    /// </summary>
    /// <param name="id">调拨单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(Transfer? Transfer, IReadOnlyList<TransferItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增单据 + 明细并持久化（同一仓储内一次 SaveChanges）
    /// </summary>
    /// <param name="transfer">单据实体</param>
    /// <param name="items">明细行</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(Transfer transfer, IReadOnlyList<TransferItem> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新单据状态（作废）并持久化（同时写入 UpdatedBy / UpdatedAt 审计字段）
    /// </summary>
    /// <param name="id">调拨单 id</param>
    /// <param name="status">目标状态</param>
    /// <param name="operatorId">操作人 id（由 Handler 取 ICurrentUser 传入，可空）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateStatusAsync(Guid id, OrderStatus status, Guid? operatorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成单号：TR + yyyyMMdd + 4 位序号（当天同前缀已有单号数 + 1）；
    /// 并发兜底由单号唯一索引承担，冲突重试由 Handler 处理（见 design.md §3.1）
    /// </summary>
    /// <param name="transferDate">业务日期（取 UTC 日期段）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<string> GenerateTransferNoAsync(DateTimeOffset transferDate, CancellationToken cancellationToken = default);
}
