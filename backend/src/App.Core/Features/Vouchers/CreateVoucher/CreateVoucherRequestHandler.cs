using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.GeneralLedger;
using App.Core.Finance;

namespace App.Core.Features.Vouchers.CreateVoucher;

/// <summary>
/// 手工凭证录入用例（一步式：录入即过账）：
/// 期间校验（不存在 40159 / 已结账 40154）→ 逐条科目校验（末级且启用，否则 40157）
/// → 借贷平衡校验（40155，与自动凭证共用同一校验）→ 同一事务：生成凭证号 + 插凭证 + 分录 + 审计
/// </summary>
public sealed class CreateVoucherRequestHandler : IRequestHandler<CreateVoucherRequest, VoucherDetailDto>
{
    /// <summary>凭证号冲突重试上限（含首次）</summary>
    private const int MaxVoucherNoAttempts = 3;

    private readonly IVoucherRepository _voucherRepository;
    private readonly IAccountingPeriodRepository _accountingPeriodRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化手工凭证录入用例处理器
    /// </summary>
    public CreateVoucherRequestHandler(
        IVoucherRepository voucherRepository,
        IAccountingPeriodRepository accountingPeriodRepository,
        IAccountRepository accountRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _voucherRepository = voucherRepository;
        _accountingPeriodRepository = accountingPeriodRepository;
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理手工凭证录入请求
    /// </summary>
    /// <param name="request">录入请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<VoucherDetailDto> HandleAsync(CreateVoucherRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new BusinessException(ErrorCode.VoucherNoEntries, "凭证至少需要一条分录");
        }

        // 查库约束：记账日期所在期间存在且未结账
        var period = await _accountingPeriodRepository.GetByYearMonthAsync(
            request.VoucherDate.Year,
            request.VoucherDate.Month,
            cancellationToken)
            ?? throw new BusinessException(
                ErrorCode.PeriodNotOpened,
                $"记账日期 {request.VoucherDate:yyyy-MM} 所在会计期间不存在，请先开账");

        if (period.Status == PeriodStatus.Closed)
        {
            throw new BusinessException(
                ErrorCode.PeriodClosed,
                $"会计期间 {period.Year}-{period.Month:00} 已结账，禁止记账");
        }

        // 查库约束：分录科目须为末级且启用
        var accounts = await _accountRepository.GetAllAsync(cancellationToken);
        var accountsById = accounts.ToDictionary(a => a.Id);
        var nonLeafIds = accounts.Where(a => a.ParentId is not null).Select(a => a.ParentId!.Value).ToHashSet();

        var voucherId = Guid.NewGuid();
        var entries = new List<VoucherEntry>(request.Items.Count);
        var lineNo = 1;
        foreach (var item in request.Items)
        {
            if (!accountsById.TryGetValue(item.AccountId, out var account))
            {
                throw new BusinessException(ErrorCode.VoucherAccountInvalid, "分录科目不存在");
            }

            if (nonLeafIds.Contains(account.Id))
            {
                throw new BusinessException(
                    ErrorCode.VoucherAccountInvalid,
                    $"科目 {account.Code} {account.Name} 非末级科目，不可记账");
            }

            if (account.Status != AccountStatus.Enabled)
            {
                throw new BusinessException(
                    ErrorCode.VoucherAccountInvalid,
                    $"科目 {account.Code} {account.Name} 已停用，不可记账");
            }

            entries.Add(new VoucherEntry
            {
                Id = SequentialGuidGenerator.NewSequential(),
                VoucherId = voucherId,
                LineNo = lineNo++,
                AccountId = account.Id,
                AccountCode = account.Code,
                AccountName = account.Name,
                Summary = string.IsNullOrWhiteSpace(item.Summary) ? null : item.Summary.Trim(),
                Debit = item.Debit,
                Credit = item.Credit,
            });
        }

        // 手工与自动凭证共用同一套借贷平衡校验
        VoucherBalanceValidator.EnsureBalanced(entries);

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        // 实体在重试循环外构建：重试时复用同一实例（改动凭证号后重新落库），避免变更跟踪残留导致重复插入
        var voucher = new Voucher
        {
            Id = voucherId,
            VoucherDate = request.VoucherDate,
            PeriodId = period.Id,
            Summary = request.Summary.Trim(),
            SourceType = VoucherSourceType.Manual,
            SourceId = null,
            SourceNo = null,
            TotalDebit = entries.Sum(e => e.Debit),
            TotalCredit = entries.Sum(e => e.Credit),
            Status = VoucherStatus.Posted,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        // 事务：生成凭证号 + 插凭证 + 分录 + 审计；凭证号冲突（唯一索引）时回滚后重新生成重试
        for (var attempt = 1; ; attempt++)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                voucher.VoucherNo = await _voucherRepository.GenerateNoAsync(request.VoucherDate, cancellationToken);
                await _voucherRepository.AddAsync(voucher, entries, cancellationToken);

                // 业务写成功后、提交前追加操作日志：与业务同事务，异常回滚则不产生日志
                var changeBuilder = new AuditChangeBuilder()
                    .Add("voucherNo", "凭证号", null, voucher.VoucherNo)
                    .Add("voucherDate", "记账日期", null, AuditSummary.Date(voucher.VoucherDate))
                    .Add("summary", "摘要", null, voucher.Summary)
                    .Add("sourceType", "来源", null, VoucherFactory.SourceTypeLabel(voucher.SourceType))
                    .Add("totalDebit", "借方合计", null, AuditSummary.Money(voucher.TotalDebit))
                    .Add("totalCredit", "贷方合计", null, AuditSummary.Money(voucher.TotalCredit))
                    .Add("entryCount", "分录行数", null, AuditSummary.Count(entries.Count));
                await _auditLogger.RecordAsync(new AuditEntry
                {
                    Resource = AuditResource.Voucher,
                    Action = AuditAction.Create,
                    ResourceId = voucher.Id,
                    ResourceNo = voucher.VoucherNo,
                    Summary = $"录入手工凭证 {voucher.VoucherNo}（摘要：{voucher.Summary}、{AuditSummary.Count(entries.Count)} 行、{AuditSummary.Money(voucher.TotalDebit)}）",
                    Changes = changeBuilder.Build(),
                    ChangesTruncated = changeBuilder.Truncated,
                    UtcNow = now,
                }, cancellationToken);

                await _unitOfWork.CommitAsync(cancellationToken);

                var (created, createdEntries) = await _voucherRepository.GetDetailAsync(voucher.Id, cancellationToken);
                if (created is null)
                {
                    throw new BusinessException(ErrorCode.NotFound, "凭证创建后读取失败");
                }

                return GeneralLedgerDtoMapper.ToVoucherDetailDto(created, createdEntries);
            }
            catch (OrderNoConflictException) when (attempt < MaxVoucherNoAttempts)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                continue;
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}
