using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.Warehouses.GetWarehouseById;

/// <summary>
/// 查询仓库详情用例：不存在返回 40400
/// </summary>
public sealed class GetWarehouseByIdRequestHandler : IRequestHandler<GetWarehouseByIdRequest, WarehouseDto>
{
    private readonly IWarehouseRepository _warehouseRepository;

    /// <summary>
    /// 初始化查询仓库详情用例处理器
    /// </summary>
    public GetWarehouseByIdRequestHandler(IWarehouseRepository warehouseRepository)
    {
        _warehouseRepository = warehouseRepository;
    }

    /// <summary>
    /// 处理查询仓库详情请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<WarehouseDto> HandleAsync(
        GetWarehouseByIdRequest request, CancellationToken cancellationToken = default)
    {
        var warehouse = await _warehouseRepository.GetByIdAsync(request.Id, cancellationToken);
        if (warehouse is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "仓库不存在");
        }

        return WarehouseDtoMapper.ToWarehouseDto(warehouse);
    }
}
