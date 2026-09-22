using App.Core.Abstractions;
using App.Core.Features.GeneralLedger;

namespace App.Core.Features.Vouchers.GetVoucherById;

/// <summary>
/// 凭证详情查询请求（路由 id）
/// </summary>
public sealed class GetVoucherByIdRequest : IRequest<VoucherDetailDto>
{
    /// <summary>凭证 id</summary>
    public Guid Id { get; init; }
}
