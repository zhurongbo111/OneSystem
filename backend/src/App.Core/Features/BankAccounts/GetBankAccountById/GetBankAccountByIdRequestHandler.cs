using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.BankAccounts.GetBankAccountById;

/// <summary>
/// 资金账户详情用例：按 id 查询，不存在返回 40400
/// </summary>
public sealed class GetBankAccountByIdRequestHandler : IRequestHandler<GetBankAccountByIdRequest, BankAccountDetailDto>
{
    private readonly IBankAccountRepository _bankAccountRepository;

    /// <summary>
    /// 初始化资金账户详情用例处理器
    /// </summary>
    public GetBankAccountByIdRequestHandler(IBankAccountRepository bankAccountRepository)
    {
        _bankAccountRepository = bankAccountRepository;
    }

    /// <summary>
    /// 处理资金账户详情请求
    /// </summary>
    /// <param name="request">详情请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<BankAccountDetailDto> HandleAsync(GetBankAccountByIdRequest request, CancellationToken cancellationToken = default)
    {
        var bankAccount = await _bankAccountRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "资金账户不存在");

        return BankAccountDtoMapper.ToBankAccountDetailDto(bankAccount);
    }
}
