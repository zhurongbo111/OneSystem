using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Accounts.CreateAccount;

/// <summary>
/// 新增会计科目用例：编码唯一 → 上级存在性 → 落库（与审计日志同事务）。
/// 新增科目不可能是预置科目（<c>IsPreset</c> 仅由种子写入）
/// </summary>
public sealed class CreateAccountRequestHandler : IRequestHandler<CreateAccountRequest, AccountDetailDto>
{
    private readonly IAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增会计科目用例处理器
    /// </summary>
    public CreateAccountRequestHandler(
        IAccountRepository accountRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理新增会计科目请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<AccountDetailDto> HandleAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        var code = request.Code.Trim();
        var name = request.Name.Trim();

        // 查库约束：编码全局唯一（大小写不敏感）
        if (await _accountRepository.ExistsByCodeAsync(code, null, cancellationToken))
        {
            throw new BusinessException(ErrorCode.AccountCodeExists, "科目编码已存在");
        }

        // 查库约束：上级科目必须存在（新建科目无下级，无需防环）
        Account? parent = null;
        if (request.ParentId is not null)
        {
            parent = await _accountRepository.GetByIdAsync(request.ParentId.Value, cancellationToken)
                ?? throw new BusinessException(ErrorCode.NotFound, "上级科目不存在");
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Category = (AccountCategory)request.Category,
            Direction = (AccountDirection)request.Direction,
            ParentId = request.ParentId,
            SortOrder = request.SortOrder,
            IsPreset = false,
            Status = (AccountStatus)request.Status,
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
            await _accountRepository.AddAsync(account, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("code", "科目编码", null, account.Code)
                .Add("name", "科目名称", null, account.Name)
                .Add("category", "科目类别", null, AuditText.AccountCategory(account.Category))
                .Add("direction", "余额方向", null, AuditText.AccountDirection(account.Direction))
                .Add("parentId", "上级科目", null, AccountDtoMapper.ParentLabel(parent))
                .Add("sortOrder", "排序", null, AuditSummary.Count(account.SortOrder))
                .Add("status", "状态", null, AuditText.AccountStatus(account.Status))
                .Add("remark", "备注", null, account.Remark);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Account,
                Action = AuditAction.Create,
                ResourceId = account.Id,
                ResourceNo = account.Code,
                Summary = $"新增科目 {account.Name}（{account.Code}）",
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

        return AccountDtoMapper.ToAccountDetailDto(account);
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}