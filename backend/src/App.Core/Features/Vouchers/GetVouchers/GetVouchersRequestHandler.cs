using App.Core.Abstractions;
using App.Core.Features.GeneralLedger;
using App.Core.Responses;

namespace App.Core.Features.Vouchers.GetVouchers;

/// <summary>
/// 凭证分页查询用例：仓储分页筛选（期间 / 来源类型 / 关键词，含作废凭证）→ 映射出参
/// </summary>
public sealed class GetVouchersRequestHandler : IRequestHandler<GetVouchersRequest, PagedResult<VoucherListItemDto>>
{
    private readonly IVoucherRepository _voucherRepository;

    /// <summary>
    /// 初始化凭证分页查询用例处理器
    /// </summary>
    public GetVouchersRequestHandler(IVoucherRepository voucherRepository)
    {
        _voucherRepository = voucherRepository;
    }

    /// <summary>
    /// 处理凭证分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<VoucherListItemDto>> HandleAsync(GetVouchersRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _voucherRepository.GetPagedAsync(
            request.Year,
            request.Month,
            request.SourceType,
            request.Keyword,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResult<VoucherListItemDto>
        {
            Items = items.Select(GeneralLedgerDtoMapper.ToVoucherListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
