using App.Core.Abstractions;

namespace App.Core.Features.Batches.GetBatchPickList;

/// <summary>
/// 批次下拉查询请求（开单页批次选择控件；按商品 + 仓返回启用批次与该仓可用库存，到期日升序）
/// </summary>
public sealed class GetBatchPickListRequest : IRequest<IReadOnlyList<BatchPickDto>>
{
    /// <summary>商品 id（必填；须已启用批次管理）</summary>
    public required Guid ProductId { get; init; }

    /// <summary>仓库 id（必填）</summary>
    public required Guid WarehouseId { get; init; }
}
