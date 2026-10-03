using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Inventory.GetInventory;

/// <summary>
/// 库存分页查询用例：关键词（编码 / 名称模糊）+ 分类筛选，按编码升序（排序 / 启用过滤在仓储内完成）；
/// 低库存标记由 Handler 计算（SafetyStock &gt; 0 且 Stock &lt; SafetyStock，阈值为 0 不提醒）；
/// 批次视图（040）的过期 / 近效期标记按固定「今天」（ISystemClock UTC 日期）计算
/// </summary>
public sealed class GetInventoryRequestHandler : IRequestHandler<GetInventoryRequest, PagedResult<InventoryItemDto>>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ISystemClock _clock;

    /// <summary>
    /// 初始化库存分页查询用例处理器
    /// </summary>
    public GetInventoryRequestHandler(IInventoryRepository inventoryRepository, ISystemClock clock)
    {
        _inventoryRepository = inventoryRepository;
        _clock = clock;
    }

    /// <summary>
    /// 处理库存分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<InventoryItemDto>> HandleAsync(GetInventoryRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _inventoryRepository.GetPagedAsync(
            request.Keyword,
            request.CategoryId,
            request.WarehouseId,
            request.BatchId,
            request.BatchNo,
            request.ExpandBatch,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResult<InventoryItemDto>
        {
            Items = items.Select(i => InventoryDtoMapper.ToInventoryItemDto(i, DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime))).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
