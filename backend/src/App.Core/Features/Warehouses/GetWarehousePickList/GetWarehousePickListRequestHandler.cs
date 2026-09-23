using App.Core.Abstractions;

namespace App.Core.Features.Warehouses.GetWarehousePickList;

/// <summary>
/// 仓库下拉查询用例：仅返回启用仓（停用仓不可被新单据选择），默认仓带标记供前端预选
/// </summary>
public sealed class GetWarehousePickListRequestHandler
    : IRequestHandler<GetWarehousePickListRequest, IReadOnlyList<WarehousePickDto>>
{
    private readonly IWarehouseRepository _warehouseRepository;

    /// <summary>
    /// 初始化仓库下拉查询用例处理器
    /// </summary>
    public GetWarehousePickListRequestHandler(IWarehouseRepository warehouseRepository)
    {
        _warehouseRepository = warehouseRepository;
    }

    /// <summary>
    /// 处理仓库下拉查询请求
    /// </summary>
    /// <param name="request">下拉查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IReadOnlyList<WarehousePickDto>> HandleAsync(
        GetWarehousePickListRequest request, CancellationToken cancellationToken = default)
    {
        var warehouses = await _warehouseRepository.GetEnabledAsync(cancellationToken);
        return warehouses.Select(WarehouseDtoMapper.ToWarehousePickDto).ToList();
    }
}
