using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 客户协议价仓储接口（实现见 App.Infrastructure；EF Core + PostgreSQL）。
/// Repository 只做数据访问，不做业务判定；一次写操作由方法自身持久化。
/// </summary>
public interface IPartnerPriceRepository
{
    /// <summary>
    /// 按 id 查询协议价，不存在返回 null（含跟踪，供编辑 / 删除）
    /// </summary>
    /// <param name="id">协议价 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<PartnerPrice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 「客户 × 商品」的协议价是否已存在；编辑时客户与商品不可改，恒传当前记录自身 id
    /// </summary>
    /// <param name="partnerId">客户 id</param>
    /// <param name="productId">商品 id</param>
    /// <param name="excludeId">需要排除的协议价 id，可空</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsAsync(Guid partnerId, Guid productId, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分页查询协议价：按客户 / 商品 / 关键词（客户名 / 商品编码 / 商品名称）筛选，创建时间倒序
    /// </summary>
    /// <param name="partnerId">客户 id，可空</param>
    /// <param name="productId">商品 id，可空</param>
    /// <param name="keyword">关键词，可空</param>
    /// <param name="page">页码，从 1 起</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前页数据与总条数</returns>
    Task<(IReadOnlyList<PartnerPriceListItem> Items, int Total)> GetPagedAsync(
        Guid? partnerId,
        Guid? productId,
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量取生效价：指定客户 + 商品 id 集合，返回每个商品的生效单价与来源（协议价优先，否则商品销售价）
    /// </summary>
    /// <param name="partnerId">客户 id</param>
    /// <param name="productIds">商品 id 集合（重复项按一次处理）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<EffectivePriceItem>> GetEffectiveAsync(
        Guid partnerId,
        IReadOnlyList<Guid> productIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增协议价并持久化
    /// </summary>
    /// <param name="partnerPrice">协议价实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(PartnerPrice partnerPrice, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新协议价并持久化
    /// </summary>
    /// <param name="partnerPrice">协议价实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(PartnerPrice partnerPrice, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 id 删除协议价并持久化；不存在时不做任何操作
    /// </summary>
    /// <param name="id">协议价 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}