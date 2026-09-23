using App.Core.Abstractions;

namespace App.Core.Features.Warehouses.SetDefaultWarehouse;

/// <summary>
/// 设为默认仓请求（全系统有且仅有一个默认仓）
/// </summary>
public sealed class SetDefaultWarehouseRequest : IRequest<WarehouseDto>
{
    /// <summary>仓库 id（由控制器从路由注入；set 供控制器赋值，不参与模型绑定）</summary>
    public Guid Id { get; set; }
}
