using App.Core.Abstractions;

namespace App.Core.Features.Warehouses.UpdateWarehouse;

/// <summary>
/// 编辑仓库请求（编码创建后不可修改，请求体不含 code；可选字段为全量覆盖语义，见 AGENTS.md §4.5）
/// </summary>
public sealed class UpdateWarehouseRequest : IRequest<WarehouseDto>
{
    /// <summary>仓库 id（由控制器从路由注入；set 供控制器赋值，不参与模型绑定）</summary>
    public Guid Id { get; set; }

    /// <summary>仓库名称（1–50 字符，唯一）</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>地址，可空（≤ 100 字符；空 / 缺省 = 清空）</summary>
    public string? Address { get; init; }

    /// <summary>联系人，可空（≤ 20 字符；空 / 缺省 = 清空）</summary>
    public string? Contact { get; init; }

    /// <summary>联系电话，可空（11 位手机号；空 / 缺省 = 清空）</summary>
    public string? Phone { get; init; }

    /// <summary>备注，可空（≤ 200 字符；空 / 缺省 = 清空）</summary>
    public string? Remark { get; init; }
}
