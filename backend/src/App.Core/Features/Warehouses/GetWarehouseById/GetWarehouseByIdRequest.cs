using App.Core.Abstractions;

namespace App.Core.Features.Warehouses.GetWarehouseById;

/// <summary>
/// 查询仓库详情请求（无格式校验，不注册 Validator）
/// </summary>
public sealed class GetWarehouseByIdRequest : IRequest<WarehouseDto>
{
    /// <summary>仓库 id</summary>
    public Guid Id { get; init; }
}
