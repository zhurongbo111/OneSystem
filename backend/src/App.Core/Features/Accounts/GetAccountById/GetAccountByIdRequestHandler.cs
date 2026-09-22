using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.Accounts.GetAccountById;

/// <summary>
/// 会计科目详情用例：按 id 查询，不存在返回 40400
/// </summary>
public sealed class GetAccountByIdRequestHandler : IRequestHandler<GetAccountByIdRequest, AccountDetailDto>
{
    private readonly IAccountRepository _accountRepository;

    /// <summary>
    /// 初始化会计科目详情用例处理器
    /// </summary>
    public GetAccountByIdRequestHandler(IAccountRepository accountRepository)
    {
        _accountRepository = accountRepository;
    }

    /// <summary>
    /// 处理会计科目详情请求
    /// </summary>
    /// <param name="request">详情请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<AccountDetailDto> HandleAsync(GetAccountByIdRequest request, CancellationToken cancellationToken = default)
    {
        var account = await _accountRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "科目不存在");

        return AccountDtoMapper.ToAccountDetailDto(account);
    }
}