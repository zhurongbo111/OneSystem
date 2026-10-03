using App.Core.Abstractions;

namespace App.Core.Features.Batches.UpdateBatch;

/// <summary>
/// 编辑批次请求（**批次号创建后不可改**，请求体不含 batchNo；可改生产日期 / 到期日 / 备注）
/// </summary>
public sealed class UpdateBatchRequest : IRequest<BatchDetailDto>
{
    /// <summary>批次 id（由控制器从路由注入；set 供控制器赋值，不参与模型绑定）</summary>
    public Guid Id { get; set; }

    /// <summary>生产日期（UTC 日期，可空；null = 清空）</summary>
    public DateTimeOffset? ProductionDate { get; init; }

    /// <summary>到期日（UTC 日期，可空；null = 永不过期）</summary>
    public DateTimeOffset? ExpiryDate { get; init; }

    /// <summary>备注（可空；空串 / 纯空白视为清空，全量覆盖语义）</summary>
    public string? Remark { get; init; }
}
