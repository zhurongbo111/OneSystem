using App.Core.Abstractions;

namespace App.Core.Features.Batches.CreateBatch;

/// <summary>
/// 新增批次请求（批次属于商品，跨仓共享；到期日与生产日期同日或晚于生产日期）
/// </summary>
public sealed class CreateBatchRequest : IRequest<BatchDetailDto>
{
    /// <summary>批次所属商品 id（必填，须已启用批次管理）</summary>
    public Guid ProductId { get; init; }

    /// <summary>批次号（1–50 位字母 / 数字 / 下划线 / 连字符，同商品内唯一，创建后不可改）</summary>
    public required string BatchNo { get; init; }

    /// <summary>生产日期（UTC 日期，可空）</summary>
    public DateTimeOffset? ProductionDate { get; init; }

    /// <summary>到期日（UTC 日期，可空；null = 永不过期）</summary>
    public DateTimeOffset? ExpiryDate { get; init; }

    /// <summary>备注（≤ 200 字符）</summary>
    public string? Remark { get; init; }
}
