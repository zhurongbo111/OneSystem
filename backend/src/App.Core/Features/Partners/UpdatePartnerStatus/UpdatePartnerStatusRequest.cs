using App.Core.Abstractions;

namespace App.Core.Features.Partners.UpdatePartnerStatus;

/// <summary>
/// 往来单位停用 / 启用请求
/// </summary>
public sealed class UpdatePartnerStatusRequest : IRequest<PartnerDto>
{
    /// <summary>往来单位 id（由控制器从路由注入；set 供控制器赋值，不参与模型绑定）</summary>
    public Guid Id { get; set; }

    /// <summary>目标状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; }
}
