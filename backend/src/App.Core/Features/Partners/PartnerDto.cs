namespace App.Core.Features.Partners;

/// <summary>
/// 往来单位出参模型（列表 / 详情 / 新增 / 编辑 / 启停共用）。
/// 枚举统一以**整型**输出（type：1 供应商 / 2 客户 / 3 两者；status：0 停用 / 1 启用），前端按整型渲染。
/// </summary>
public sealed class PartnerDto
{
    /// <summary>往来单位 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>单位名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>单位类型（1 供应商 / 2 客户 / 3 两者）</summary>
    public int Type { get; init; }

    /// <summary>联系人</summary>
    public string? Contact { get; init; }

    /// <summary>联系电话</summary>
    public string? Phone { get; init; }

    /// <summary>地址</summary>
    public string? Address { get; init; }

    /// <summary>备注</summary>
    public string? Remark { get; init; }

    /// <summary>单位状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
