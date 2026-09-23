using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Inventory.GetInventory;

/// <summary>
/// 库存分页查询用例：关键词（编码 / 名称模糊）+ 分类筛选，按编码升序（排序 / 启用过滤在仓储内完成）；
/// 低库存标记由 Handler 计算（SafetyStock &gt; 0 且 Stock &lt; SafetyStock，阈值为 0 不提醒）
/// </summary>
public sealed class GetInventoryRequestHandler : IRequestHandler<GetInventoryRequest, PagedResult<InventoryItemDto>>
{
    private readonly IInventoryRepository _inventoryRepository;

    /// <summary>
    /// 初始化库存分页查询用例处理器
    /// </summary>
    public GetInventoryRequestHandler(IInventoryRepository inventoryRepository)
    {
        _inventoryRepository = inventoryRepository;
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
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResult<InventoryItemDto>
        {
            Items = items.Select(InventoryDtoMapper.ToInventoryItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
