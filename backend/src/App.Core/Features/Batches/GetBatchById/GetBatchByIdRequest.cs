using App.Core.Abstractions;

namespace App.Core.Features.Batches.GetBatchById;

/// <summary>
/// 查询批次详情请求（含已停用批次；批次号 / 生产日期 / 到期日 / 备注）
/// </summary>
public sealed class GetBatchByIdRequest : IRequest<BatchDetailDto>
{
    /// <summary>批次 id（由控制器从路由注入；set 供控制器赋值，不参与模型绑定）</summary>
    public Guid Id { get; set; }
}
