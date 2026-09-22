using App.Core.Abstractions;
using App.Core.Features.GeneralLedger;

namespace App.Core.Features.Vouchers.VoidVoucher;

/// <summary>
/// 凭证作废请求（路由 id）
/// </summary>
public sealed class VoidVoucherRequest : IRequest<VoucherDetailDto>
{
    /// <summary>凭证 id</summary>
    public Guid Id { get; init; }
}
