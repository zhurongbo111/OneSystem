using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Accounts.UpdateAccount;

/// <summary>
/// 编辑会计科目用例：存在性 → 编码唯一 → 上级存在性与防环（不得为自身或自身下级）
/// → 更新（与审计日志同事务）。<c>IsPreset</c> 不可改（保持原值）
/// </summary>
public sealed class UpdateAccountRequestHandler : IRequestHandler<UpdateAccountRequest, AccountDetailDto>
{
    private readonly IAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化编辑会计科目用例处理器
    /// </summary>
    public UpdateAccountRequestHandler(
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
    /// 处理编辑会计科目请求
    /// </summary>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<AccountDetailDto> HandleAsync(UpdateAccountRequest request, CancellationToken cancellationToken = default)
    {
        var account = await _accountRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "科目不存在");

        var code = request.Code.Trim();
        var name = request.Name.Trim();

        // 查库约束：编码全局唯一（排除自身）
        if (await _accountRepository.ExistsByCodeAsync(code, account.Id, cancellationToken))
        {
            throw new BusinessException(ErrorCode.AccountCodeExists, "科目编码已存在");
        }

        // 全量科目：同时用于「上级科目」展示文本与防环上溯（科目表量级小，一次取全量）
        var accounts = await _accountRepository.GetAllAsync(cancellationToken);

        // 查库约束：上级存在性 + 防环（不得为自身或自身下级；沿父链上溯，命中即防环失败）
        if (request.ParentId != account.ParentId)
        {
            if (request.ParentId == account.Id)
            {
                throw new BusinessException(ErrorCode.DepartmentCycle, "上级科目不能是自身或其下级");
            }

            if (request.ParentId is not null && !accounts.Any(a => a.Id == request.ParentId.Value))
            {
                throw new BusinessException(ErrorCode.NotFound, "上级科目不存在");
            }

            if (request.ParentId is not null && IsSelfOrDescendant(accounts, account.Id, request.ParentId.Value))
            {
                throw new BusinessException(ErrorCode.DepartmentCycle, "上级科目不能是自身或其下级");
            }
        }

        var beforeCode = account.Code;
        var beforeName = account.Name;
        var beforeCategory = account.Category;
        var beforeDirection = account.Direction;
        var beforeParentLabel = ParentLabelOf(accounts, account.ParentId);
        var beforeSortOrder = account.SortOrder;
        var beforeStatus = account.Status;
        var beforeRemark = account.Remark;

        var now = DateTimeOffset.UtcNow;
        account.Code = code;
        account.Name = name;
        account.Category = (AccountCategory)request.Category;
        account.Direction = (AccountDirection)request.Direction;
        account.ParentId = request.ParentId;
        account.SortOrder = request.SortOrder;
        account.Status = (AccountStatus)request.Status;
        account.Remark = NullIfWhiteSpace(request.Remark);
        account.UpdatedAt = now;
        account.UpdatedBy = _currentUser.UserId();

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _accountRepository.UpdateAsync(account, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("code", "科目编码", beforeCode, account.Code)
                .Add("name", "科目名称", beforeName, account.Name)
                .Add("category", "科目类别", AuditText.AccountCategory(beforeCategory), AuditText.AccountCategory(account.Category))
                .Add("direction", "余额方向", AuditText.AccountDirection(beforeDirection), AuditText.AccountDirection(account.Direction))
                .Add("parentId", "上级科目", beforeParentLabel, ParentLabelOf(accounts, account.ParentId))
                .Add("sortOrder", "排序", AuditSummary.Count(beforeSortOrder), AuditSummary.Count(account.SortOrder))
                .Add("status", "状态", AuditText.AccountStatus(beforeStatus), AuditText.AccountStatus(account.Status))
                .Add("remark", "备注", beforeRemark, account.Remark);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Account,
                Action = AuditAction.Update,
                ResourceId = account.Id,
                ResourceNo = account.Code,
                Summary = $"修改科目 {account.Name}（{account.Code}）",
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

    /// <summary>
    /// 判定候选上级是否位于当前科目子树内（自身或下级）：把全量科目拍平为「科目 id → 上级 id」映射后沿父链上溯
    /// </summary>
    private static bool IsSelfOrDescendant(IReadOnlyList<Account> accounts, Guid accountId, Guid candidateParentId)
    {
        var parentById = accounts.ToDictionary(a => a.Id, a => a.ParentId);

        Guid? cursor = candidateParentId;
        while (cursor is not null)
        {
            if (cursor.Value == accountId)
            {
                return true;
            }

            cursor = parentById.TryGetValue(cursor.Value, out var parentId) ? parentId : null;
        }

        return false;
    }

    /// <summary>按 id 取上级科目的展示文本（编码 + 名称）；无上级或未找到输出 <c>null</c></summary>
    private static string? ParentLabelOf(IReadOnlyList<Account> accounts, Guid? parentId)
        => parentId is null ? null : AccountDtoMapper.ParentLabel(accounts.FirstOrDefault(a => a.Id == parentId.Value));

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}