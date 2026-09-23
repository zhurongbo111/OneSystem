using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.BankAccounts.UpdateBankAccount;

/// <summary>
/// 编辑资金账户用例：存在性 → 编码唯一 → 全量覆盖更新（与审计日志同事务）
/// </summary>
public sealed class UpdateBankAccountRequestHandler : IRequestHandler<UpdateBankAccountRequest, BankAccountDetailDto>
{
    private readonly IBankAccountRepository _bankAccountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化编辑资金账户用例处理器
    /// </summary>
    public UpdateBankAccountRequestHandler(
        IBankAccountRepository bankAccountRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _bankAccountRepository = bankAccountRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理编辑资金账户请求
    /// </summary>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<BankAccountDetailDto> HandleAsync(UpdateBankAccountRequest request, CancellationToken cancellationToken = default)
    {
        var bankAccount = await _bankAccountRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "资金账户不存在");

        var code = request.Code.Trim();
        var name = request.Name.Trim();

        // 查库约束：编码全局唯一（排除自身）
        if (await _bankAccountRepository.ExistsByCodeAsync(code, bankAccount.Id, cancellationToken))
        {
            throw new BusinessException(ErrorCode.BankAccountCodeExists, "资金账户编码已存在");
        }

        var beforeCode = bankAccount.Code;
        var beforeName = bankAccount.Name;
        var beforeType = bankAccount.Type;
        var beforeBankName = bankAccount.BankName;
        var beforeAccountNo = bankAccount.AccountNo;
        var beforeInitialBalance = bankAccount.InitialBalance;
        var beforeStatus = bankAccount.Status;
        var beforeRemark = bankAccount.Remark;

        var type = (BankAccountType)request.Type;
        var isBank = type == BankAccountType.Bank;

        var now = DateTimeOffset.UtcNow;
        bankAccount.Code = code;
        bankAccount.Name = name;
        bankAccount.Type = type;
        bankAccount.BankName = isBank ? NullIfWhiteSpace(request.BankName) : null;
        bankAccount.AccountNo = isBank ? NullIfWhiteSpace(request.AccountNo) : null;
        bankAccount.InitialBalance = request.InitialBalance;
        bankAccount.Status = (BankAccountStatus)request.Status;
        bankAccount.Remark = NullIfWhiteSpace(request.Remark);
        bankAccount.UpdatedAt = now;
        bankAccount.UpdatedBy = _currentUser.UserId();

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _bankAccountRepository.UpdateAsync(bankAccount, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("code", "账户编码", beforeCode, bankAccount.Code)
                .Add("name", "账户名称", beforeName, bankAccount.Name)
                .Add("type", "账户类型", AuditText.BankAccountType(beforeType), AuditText.BankAccountType(bankAccount.Type))
                .Add("bankName", "开户行", beforeBankName, bankAccount.BankName)
                .Add("accountNo", "银行账号", beforeAccountNo, bankAccount.AccountNo)
                .Add("initialBalance", "初始余额", AuditSummary.Money(beforeInitialBalance), AuditSummary.Money(bankAccount.InitialBalance))
                .Add("status", "状态", AuditText.BankAccountStatus(beforeStatus), AuditText.BankAccountStatus(bankAccount.Status))
                .Add("remark", "备注", beforeRemark, bankAccount.Remark);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.BankAccount,
                Action = AuditAction.Update,
                ResourceId = bankAccount.Id,
                ResourceNo = bankAccount.Code,
                Summary = $"修改资金账户 {bankAccount.Name}（{bankAccount.Code}）",
                Changes = changeBuilder.Build(),
                ChangesTruncated = changeBuilder.Truncated,
                UtcNow = now,
            }, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        return BankAccountDtoMapper.ToBankAccountDetailDto(bankAccount);
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
