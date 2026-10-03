using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 批次档案仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL，040-erp-batch-expiry）。
/// 批次属于**商品**（ProductId + BatchNo 同商品内唯一），跨仓共享；停用不删除。
/// 过期 / 近效期的**判定**在 Handler 按固定「今天」计算（仓储只返回原始日期）。
/// </summary>
public interface IBatchRepository
{
    /// <summary>
    /// 按 id 查询批次（含已停用；不存在返回 null）
    /// </summary>
    /// <param name="id">批次 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<Batch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按商品 + 批次号精确查询批次（不存在返回 null；就地新建批次幂等复选用）
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="batchNo">批次号</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<Batch?> GetByProductAndBatchNoAsync(
        Guid productId, string batchNo, CancellationToken cancellationToken = default);

    /// <summary>
    /// 同商品内批次号是否已存在（**大小写不敏感**判定；数据库唯一索引大小写敏感，忽略大小写的判定由本方法负责）
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="batchNo">批次号</param>
    /// <param name="excludeId">需排除的批次 id（更新场景排除自身，可空）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsByBatchNoAsync(
        Guid productId, string batchNo, Guid? excludeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次分页查询（批次列表页）：keyword 模糊匹配 BatchNo；productId / status 精确匹配（可空）；
    /// <c>onlyExpiring</c> 为 true 时只返回近效期或已过期批次（到期日非空，判定窗口由入参 today 计算，Handler 传入）；
    /// 按 CreatedAt 倒序；<c>AsNoTracking</c>。联查 Products / Inventory 带出商品编码 / 名称与跨仓库存合计。
    /// </summary>
    /// <param name="keyword">批次号关键词，可空</param>
    /// <param name="productId">商品 id，可空</param>
    /// <param name="status">批次状态，可空</param>
    /// <param name="onlyExpiring">是否只看近效期 / 已过期，可空（false / null = 不过滤）</param>
    /// <param name="today">「今天」（UTC 日期粒度，Handler 注入，判定窗口 = today + NearExpiryDays）</param>
    /// <param name="page">页码（从 1 起）</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<(IReadOnlyList<BatchListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? productId,
        PartnerStatus? status,
        bool? onlyExpiring,
        DateTimeOffset today,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批次下拉查询（开单页批次选择控件）：返回该商品「**有库存或未过期**」的启用批次（已过期的批次仅在仍有库存时保留），
    /// 按 <c>ExpiryDate</c> 升序（无到期日最后）；联查 Inventory 带出该仓可用库存（批次在该仓无库存行为 0）。
    /// </summary>
    /// <param name="productId">商品 id</param>
    /// <param name="warehouseId">仓库 id</param>
    /// <param name="today">「今天」（UTC 日期粒度，Handler 注入，过期判定用）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<BatchPickItem>> GetPickListAsync(
        Guid productId, Guid warehouseId, DateTimeOffset today, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增批次（批次号唯一性 / 商品状态等校验由 Handler 负责）
    /// </summary>
    /// <param name="batch">批次实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(Batch batch, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新批次（仅生产 / 到期日 / 备注 / 状态可改；**批次号不可改**，由 Handler 负责）
    /// </summary>
    /// <param name="batch">批次实体（Id 已设置）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(Batch batch, CancellationToken cancellationToken = default);
}
