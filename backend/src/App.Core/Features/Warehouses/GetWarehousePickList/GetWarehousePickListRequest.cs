using App.Core.Abstractions;

namespace App.Core.Features.Warehouses.GetWarehousePickList;

/// <summary>
/// 仓库下拉查询请求（开单页用；无参数，不注册 Validator）
/// </summary>
public sealed class GetWarehousePickListRequest : IRequest<IReadOnlyList<WarehousePickDto>>
{
}
