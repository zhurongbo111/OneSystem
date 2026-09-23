namespace App.Core.Features.Warehouses;

/// <summary>
/// 仓库下拉项出参（开单页「仓库」下拉数据源；仅启用仓，默认仓供前端预选）
/// </summary>
public sealed class WarehousePickDto
{
    /// <summary>仓库 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>仓库编码</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>仓库名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>是否默认仓（前端预选）</summary>
    public bool IsDefault { get; init; }
}
