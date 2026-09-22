using App.Core.Abstractions;
using App.Core.Errors;
using App.Core.Features.GeneralLedger;

namespace App.Core.Features.Vouchers.GetVoucherById;

/// <summary>
/// 凭证详情查询用例：不存在 → 40400；返回主表 + 分录
/// </summary>
public sealed class GetVoucherByIdRequestHandler : IRequestHandler<GetVoucherByIdRequest, VoucherDetailDto>
{
    private readonly IVoucherRepository _voucherRepository;

    /// <summary>
    /// 初始化凭证详情查询用例处理器
    /// </summary>
    public GetVoucherByIdRequestHandler(IVoucherRepository voucherRepository)
    {
        _voucherRepository = voucherRepository;
    }

    /// <summary>
    /// 处理凭证详情查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<VoucherDetailDto> HandleAsync(GetVoucherByIdRequest request, CancellationToken cancellationToken = default)
    {
        var (voucher, entries) = await _voucherRepository.GetDetailAsync(request.Id, cancellationToken);
        if (voucher is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "凭证不存在");
        }

        return GeneralLedgerDtoMapper.ToVoucherDetailDto(voucher, entries);
    }
}
