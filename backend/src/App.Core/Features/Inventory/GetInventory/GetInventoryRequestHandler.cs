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
            request.Page,
            request.PageSize,
            cancellationToken);

        var pageItems = items.Select(ToDto).ToList();
        return new PagedResult<InventoryItemDto>
        {
            Items = pageItems,
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }

    /// <summary>
    /// 列表项读模型转出参模型，计算低库存标记
    /// </summary>
    private static InventoryItemDto ToDto(InventoryItem item)
        => new()
        {
            ProductId = item.ProductId.ToString(),
            Code = item.Code,
            Name = item.Name,
            CategoryName = item.CategoryName,
            Unit = item.Unit,
            StockQuantity = item.StockQuantity,
            SafetyStock = item.SafetyStock,
            IsBelowSafetyStock = item.SafetyStock > 0 && item.StockQuantity < item.SafetyStock,
            UpdatedAt = item.UpdatedAt,
        };
}
