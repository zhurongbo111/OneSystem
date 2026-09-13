using App.Core.Abstractions;

namespace App.Core.Features.Partners.GetPartnerById;

/// <summary>
/// 查询往来单位详情请求（无格式校验，不注册 Validator）
/// </summary>
public sealed class GetPartnerByIdRequest : IRequest<PartnerDto>
{
    /// <summary>往来单位 id</summary>
    public Guid Id { get; init; }
}
