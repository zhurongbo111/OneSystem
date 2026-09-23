namespace App.Core.Features.Warehouses;

/// <summary>
/// 仓库出参模型（列表 / 详情 / 新增 / 编辑 / 启停 / 设为默认共用）。
/// 状态以**整型**输出（0 停用 / 1 启用），前端按整型渲染。
/// </summary>
public sealed class WarehouseDto
{
    /// <summary>仓库 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>仓库编码（创建后不可修改）</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>仓库名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>地址</summary>
    public string? Address { get; init; }

    /// <summary>联系人</summary>
    public string? Contact { get; init; }

    /// <summary>联系电话</summary>
    public string? Phone { get; init; }

    /// <summary>是否默认仓</summary>
    public bool IsDefault { get; init; }

    /// <summary>仓库状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; }

    /// <summary>备注</summary>
    public string? Remark { get; init; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
