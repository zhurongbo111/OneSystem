using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 记账凭证仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// Repository 只做数据访问，不做业务判定；借贷平衡 / 科目合法性 / 期间控制在 Handler 与共享组件
/// （specs/033-erp-general-ledger/design.md §3.1）。
/// </summary>
public interface IVoucherRepository
{
    /// <summary>
    /// 生成并追加一张凭证（含分录），同一事务内落库。
    /// 凭证号唯一约束冲突由实现包装为 <see cref="App.Core.Errors.OrderNoConflictException"/>，由调用方重试
    /// </summary>
    /// <param name="voucher">凭证实体（需已填 <c>VoucherNo</c>）</param>
    /// <param name="entries">分录集合（借方合计须等于贷方合计）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(Voucher voucher, IReadOnlyList<VoucherEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>
    /// 凭证分页查询（含作废凭证，列表投影排除分录大字段）：期间 / 来源类型 / 关键词（凭证号或摘要）
    /// </summary>
    /// <param name="year">归属期间年，可空</param>
    /// <param name="month">归属期间月，可空</param>
    /// <param name="sourceType">来源类型，可空</param>
    /// <param name="keyword">凭证号 / 摘要关键词，可空</param>
    /// <param name="page">页码（从 1 起）</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<Voucher> Items, int Total)> GetPagedAsync(
        int? year,
        int? month,
        VoucherSourceType? sourceType,
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 凭证详情（主表 + 分录，分录按行号升序），不存在返回 <c>(null, 空集合)</c>
    /// </summary>
    /// <param name="id">凭证 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(Voucher? Voucher, IReadOnlyList<VoucherEntry> Entries)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 作废凭证（仅改状态，不删数据；已作废的凭证跳过）
    /// </summary>
    /// <param name="id">凭证 id</param>
    /// <param name="operatorId">操作人用户 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实际被作废的凭证数（0 表示无凭证或已作废）</returns>
    Task<int> VoidAsync(Guid id, Guid? operatorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按来源单据作废其全部自动凭证（单据作废时同事务调用）；已作废的跳过
    /// </summary>
    /// <param name="sourceType">来源类型</param>
    /// <param name="sourceId">来源单据 id</param>
    /// <param name="operatorId">操作人用户 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实际被作废的凭证数</returns>
    Task<int> VoidBySourceAsync(VoucherSourceType sourceType, Guid sourceId, Guid? operatorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成凭证号（<c>记-YYYYMM-NNNN</c>，按期间内序号自增；唯一索引兜底并发冲突）
    /// </summary>
    /// <param name="voucherDate">记账日期（决定归属期间）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<string> GenerateNoAsync(DateTimeOffset voucherDate, CancellationToken cancellationToken = default);
}
