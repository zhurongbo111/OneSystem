using App.Core.Abstractions;

namespace App.Core.Features.Warehouses.CreateWarehouse;

/// <summary>
/// 新增仓库请求（编码创建后不可修改；默认启用、非默认仓，设为默认另走专用接口）
/// </summary>
public sealed class CreateWarehouseRequest : IRequest<WarehouseDto>
{
    /// <summary>仓库编码（2–20 位字母 / 数字 / 下划线 / 连字符，唯一，创建后不可修改）</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>仓库名称（1–50 字符，唯一）</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>地址，可空（≤ 100 字符）</summary>
    public string? Address { get; init; }

    /// <summary>联系人，可空（≤ 20 字符）</summary>
    public string? Contact { get; init; }

    /// <summary>联系电话，可空（11 位手机号）</summary>
    public string? Phone { get; init; }

    /// <summary>备注，可空（≤ 200 字符）</summary>
    public string? Remark { get; init; }
}
