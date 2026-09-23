using App.Core.Abstractions;

namespace App.Core.Features.Warehouses.UpdateWarehouseStatus;

/// <summary>
/// 仓库停用 / 启用请求（默认仓不可停用，返回 40124）
/// </summary>
public sealed class UpdateWarehouseStatusRequest : IRequest<WarehouseDto>
{
    /// <summary>仓库 id（由控制器从路由注入；set 供控制器赋值，不参与模型绑定）</summary>
    public Guid Id { get; set; }

    /// <summary>目标状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; }
}
