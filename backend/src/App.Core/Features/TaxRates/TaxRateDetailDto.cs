namespace App.Core.Features.TaxRates;

/// <summary>
/// 税率详情出参模型（详情 / 新增 / 编辑 / 启停共用）。
/// 枚举统一以**整型**输出（<c>TaxRateStatus</c>：0 停用 / 1 启用），前端按整型渲染。
/// </summary>
public sealed class TaxRateDetailDto
{
    /// <summary>税率 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>税率编码</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>税率名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>税率百分比数值（<c>13</c> 表示 13%）</summary>
    public decimal Rate { get; init; }

    /// <summary>税率状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; }

    /// <summary>备注</summary>
    public string? Remark { get; init; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}