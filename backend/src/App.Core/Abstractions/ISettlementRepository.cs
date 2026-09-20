using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 收付款单仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// Repository 只做数据访问，不做业务判定；主表 + 核销明细的一次写操作由仓储自身持久化。
/// 跨仓储写（收付款单 + 逐张单据已结算金额累加）由 Handler 用 IUnitOfWork 包成同一事务。
/// </summary>
public interface ISettlementRepository
{
    /// <summary>
    /// 分页查询收付款单：单号 / 往来名称关键词 + 类型 + 往来 + 方式 + 业务日期闭区间，创建时间倒序；含作废单据
    /// </summary>
    /// <param name="keyword">单号 / 往来名称关键词，可空</param>
    /// <param name="type">类型（0 收款 / 1 付款），可空</param>
    /// <param name="partnerId">往来单位 id，可空</param>
    /// <param name="method">方式，可空</param>
    /// <param name="start">起始业务日期（含），可空</param>
    /// <param name="end">结束业务日期（含），可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<Settlement> Items, int Total)> GetPagedAsync(
        string? keyword,
        SettlementType? type,
        Guid? partnerId,
        SettlementMethod? method,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询收付款单详情（主表实体 + 核销明细，按明细 Id 还原插入顺序），不存在时 Settlement 为 null
    /// </summary>
    /// <param name="id">收付款单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(Settlement? Settlement, IReadOnlyList<SettlementItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增收付款单 + 核销明细并持久化（同一仓储内一次 SaveChanges）
    /// </summary>
    /// <param name="settlement">收付款单实体</param>
    /// <param name="items">核销明细行</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(Settlement settlement, IReadOnlyList<SettlementItem> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新单据状态（作废）并持久化（同时写入 UpdatedBy / UpdatedAt 审计字段）
    /// </summary>
    /// <param name="id">收付款单 id</param>
    /// <param name="status">目标状态</param>
    /// <param name="operatorId">操作人 id（由 Handler 取 ICurrentUser 传入，可空）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateStatusAsync(Guid id, OrderStatus status, Guid? operatorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成单号：前缀 + yyyyMMdd + 4 位序号（当天同前缀已有单号数 + 1）；
    /// 前缀按类型取 RC（收款）/ PY（付款），并发兜底由单号唯一索引承担，冲突重试由 Handler 处理
    /// </summary>
    /// <param name="type">收付款单类型</param>
    /// <param name="settlementDate">业务日期（取 UTC 日期段）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<string> GenerateSettlementNoAsync(SettlementType type, DateTimeOffset settlementDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按单据 id 集合批量查询核销明细行（erp-export 导出用，一次查询避免逐单 N+1），按明细 Id 升序（插入顺序）
    /// </summary>
    /// <param name="settlementIds">收付款单 id 集合（空集合返回空列表）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<SettlementItem>> GetItemsBySettlementIdsAsync(IReadOnlyCollection<Guid> settlementIds, CancellationToken cancellationToken = default);
}
