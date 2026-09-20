using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 盘点 / 期初建账单据仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// 只做数据访问，不做业务判定；审计字段由 Handler 经 ICurrentUser 获取后随实体 / 参数传入，仓储不感知当前用户。
/// 跨仓储写（单据 + 明细 + 库存 + 流水）由调用方 IUnitOfWork 包成同一事务。
/// </summary>
public interface IStockTakeRepository
{
    /// <summary>
    /// 新增单据 + 明细（同一仓储内一次 SaveChangesAsync；明细行顺序由明细 Id 顺序 Guid 保证）。
    /// 单号唯一约束冲突（PostgreSQL 23505）包装为 <see cref="Errors.OrderNoConflictException"/> 抛出，由 Handler 重试。
    /// </summary>
    /// <param name="take">盘点单实体（含已生成的 TakeNo 与统计字段）</param>
    /// <param name="items">明细行（含快照与差异）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(StockTake take, IReadOnlyList<StockTakeItem> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// 盘点单分页查询：keyword 模糊匹配单号 TakeNo；type 精确匹配；start / end 对 TakeDate 闭区间；
    /// 按 CreatedAt 倒序；AsNoTracking，直接返回实体。
    /// </summary>
    /// <param name="keyword">单号关键词，可空</param>
    /// <param name="type">单据类型，可空</param>
    /// <param name="start">盘点日期起（UTC），可空</param>
    /// <param name="end">盘点日期止（UTC），可空</param>
    /// <param name="page">页码（从 1 起）</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<StockTake> Items, int Total)> GetPagedAsync(
        string? keyword,
        StockTakeType? type,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 盘点单详情：主表 + 明细（按明细插入顺序返回），不存在时主表为 null。
    /// </summary>
    /// <param name="id">盘点单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(StockTake? Take, IReadOnlyList<StockTakeItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成盘点单号（ST + yyyyMMdd + 4 位序号，如 ST202609160001）。
    /// 机制与实现对齐 erp-purchase §3.6：COUNT(*) 当天同前缀 + 1 补零，唯一索引兜底并发冲突（Handler 重试）。
    /// </summary>
    /// <param name="takeDate">盘点业务日期</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<string> GenerateTakeNoAsync(DateTimeOffset takeDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按单据 id 集合批量查询明细行（erp-export 导出用，一次查询避免逐单 N+1），按明细 Id 升序（插入顺序）
    /// </summary>
    /// <param name="takeIds">盘点单 id 集合（空集合返回空列表）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<StockTakeItem>> GetItemsByTakeIdsAsync(IReadOnlyCollection<Guid> takeIds, CancellationToken cancellationToken = default);
}
