using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.BankAccounts.CreateBankAccount;

/// <summary>
/// 新增资金账户用例：编码唯一 → 落库（与审计日志同事务）。
/// 账户类型与结算方式的匹配由收付款单（`023`）在开单时校验，本用例只存账户本体
/// </summary>
public sealed class CreateBankAccountRequestHandler : IRequestHandler<CreateBankAccountRequest, BankAccountDetailDto>
{
    private readonly IBankAccountRepository _bankAccountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增资金账户用例处理器
    /// </summary>
    public CreateBankAccountRequestHandler(
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
    /// 处理新增资金账户请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<BankAccountDetailDto> HandleAsync(CreateBankAccountRequest request, CancellationToken cancellationToken = default)
    {
        var code = request.Code.Trim();
        var name = request.Name.Trim();

        // 查库约束：编码全局唯一（大小写不敏感）
        if (await _bankAccountRepository.ExistsByCodeAsync(code, null, cancellationToken))
        {
            throw new BusinessException(ErrorCode.BankAccountCodeExists, "资金账户编码已存在");
        }

        var type = (BankAccountType)request.Type;
        var isBank = type == BankAccountType.Bank;

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        var bankAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Type = type,
            // 现金账户不落开户行 / 账号（前端表单隐藏，此处统一收敛，避免脏快照）
            BankName = isBank ? NullIfWhiteSpace(request.BankName) : null,
            AccountNo = isBank ? NullIfWhiteSpace(request.AccountNo) : null,
            InitialBalance = request.InitialBalance,
            Status = (BankAccountStatus)request.Status,
            Remark = NullIfWhiteSpace(request.Remark),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _bankAccountRepository.AddAsync(bankAccount, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("code", "账户编码", null, bankAccount.Code)
                .Add("name", "账户名称", null, bankAccount.Name)
                .Add("type", "账户类型", null, AuditText.BankAccountType(bankAccount.Type))
                .Add("bankName", "开户行", null, bankAccount.BankName)
                .Add("accountNo", "银行账号", null, bankAccount.AccountNo)
                .Add("initialBalance", "初始余额", null, AuditSummary.Money(bankAccount.InitialBalance))
                .Add("status", "状态", null, AuditText.BankAccountStatus(bankAccount.Status))
                .Add("remark", "备注", null, bankAccount.Remark);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.BankAccount,
                Action = AuditAction.Create,
                ResourceId = bankAccount.Id,
                ResourceNo = bankAccount.Code,
                Summary = $"新增资金账户 {bankAccount.Name}（{bankAccount.Code}）",
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
