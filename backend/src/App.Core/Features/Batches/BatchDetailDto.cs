namespace App.Core.Features.Batches;

/// <summary>
/// 批次详情 / 新增 / 编辑 / 启停共用出参（列表用 <c>BatchListItemDto</c>，下拉用 <c>BatchPickDto</c>）。
/// 枚举统一以**整型**输出（status：0 停用 / 1 启用），前端按整型渲染。
/// </summary>
public sealed class BatchDetailDto
{
    /// <summary>批次 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>批次所属商品 ID</summary>
    public string ProductId { get; init; } = string.Empty;

    /// <summary>商品编码（联查带出）</summary>
    public string ProductCode { get; init; } = string.Empty;

    /// <summary>商品名称（联查带出）</summary>
    public string ProductName { get; init; } = string.Empty;

    /// <summary>批次号（创建后不可改）</summary>
    public string BatchNo { get; init; } = string.Empty;

    /// <summary>生产日期（UTC 午夜，可空）</summary>
    public DateTimeOffset? ProductionDate { get; init; }

    /// <summary>到期日（UTC 午夜，可空；null = 永不过期）</summary>
    public DateTimeOffset? ExpiryDate { get; init; }

    /// <summary>批次状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; }

    /// <summary>备注</summary>
    public string? Remark { get; init; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
