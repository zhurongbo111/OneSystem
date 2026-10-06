using App.Core.Abstractions;

namespace App.Core.Features.Batches.UpdateBatchStatus;

/// <summary>
/// 批次停用 / 启用请求（停用后不可用于新的出入库单；停用不删除，保留历史单据与流水引用）
/// </summary>
public sealed class UpdateBatchStatusRequest : IRequest<BatchDetailDto>
{
    /// <summary>批次 id（由控制器从路由注入；set 供控制器赋值，不参与模型绑定）</summary>
    public Guid Id { get; set; }

    /// <summary>目标状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; }
}
