using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.BankAccounts.GetBankAccounts;

/// <summary>
/// 资金账户分页列表用例：按关键词 / 类型 / 状态筛选后分页查询，直接依赖仓储，无 Service 层
/// </summary>
public sealed class GetBankAccountsRequestHandler : IRequestHandler<GetBankAccountsRequest, PagedResult<BankAccountListItemDto>>
{
    private readonly IBankAccountRepository _bankAccountRepository;

    /// <summary>
    /// 初始化资金账户分页列表用例处理器
    /// </summary>
    public GetBankAccountsRequestHandler(IBankAccountRepository bankAccountRepository)
    {
        _bankAccountRepository = bankAccountRepository;
    }

    /// <summary>
    /// 处理资金账户分页列表请求
    /// </summary>
    /// <param name="request">列表请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<BankAccountListItemDto>> HandleAsync(GetBankAccountsRequest request, CancellationToken cancellationToken = default)
    {
        var type = request.Type is null ? (BankAccountType?)null : (BankAccountType)request.Type.Value;
        var status = request.Status is null ? (BankAccountStatus?)null : (BankAccountStatus)request.Status.Value;

        var (items, total) = await _bankAccountRepository.GetPagedAsync(
            request.Keyword,
            type,
            status,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResult<BankAccountListItemDto>
        {
            Items = items.Select(BankAccountDtoMapper.ToBankAccountListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
