using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Warehouses.GetWarehouses;

/// <summary>
/// 仓库分页查询用例：关键词（编码 / 名称模糊）+ 状态筛选，默认仓置顶、同组按编码升序
/// </summary>
public sealed class GetWarehousesRequestHandler : IRequestHandler<GetWarehousesRequest, PagedResult<WarehouseDto>>
{
    private readonly IWarehouseRepository _warehouseRepository;

    /// <summary>
    /// 初始化仓库分页查询用例处理器
    /// </summary>
    public GetWarehousesRequestHandler(IWarehouseRepository warehouseRepository)
    {
        _warehouseRepository = warehouseRepository;
    }

    /// <summary>
    /// 处理仓库分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<WarehouseDto>> HandleAsync(
        GetWarehousesRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _warehouseRepository.GetPagedAsync(
            request.Keyword,
            request.Status,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResult<WarehouseDto>
        {
            Items = items.Select(WarehouseDtoMapper.ToWarehouseDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
