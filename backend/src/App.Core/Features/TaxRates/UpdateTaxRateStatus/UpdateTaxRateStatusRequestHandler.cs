using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.TaxRates.UpdateTaxRateStatus;

/// <summary>
/// 税率停用 / 启用用例：存在性 → 更新状态（与审计日志同事务）。
/// 停用税率不可被新发票 / 凭证引用，历史数据保留
/// </summary>
public sealed class UpdateTaxRateStatusRequestHandler : IRequestHandler<UpdateTaxRateStatusRequest, TaxRateDetailDto>
{
    private readonly ITaxRateRepository _taxRateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化税率停用 / 启用用例处理器
    /// </summary>
    public UpdateTaxRateStatusRequestHandler(
        ITaxRateRepository taxRateRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _taxRateRepository = taxRateRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理税率停用 / 启用请求
    /// </summary>
    /// <param name="request">停用 / 启用请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<TaxRateDetailDto> HandleAsync(UpdateTaxRateStatusRequest request, CancellationToken cancellationToken = default)
    {
        var taxRate = await _taxRateRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "税率不存在");

        var beforeStatus = taxRate.Status;
        var now = DateTimeOffset.UtcNow;
        taxRate.Status = (TaxRateStatus)request.Status;
        taxRate.UpdatedAt = now;
        taxRate.UpdatedBy = _currentUser.UserId();

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _taxRateRepository.UpdateAsync(taxRate, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("status", "状态", AuditText.TaxRateStatus(beforeStatus), AuditText.TaxRateStatus(taxRate.Status));
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.TaxRate,
                Action = AuditAction.StatusChange,
                ResourceId = taxRate.Id,
                ResourceNo = taxRate.Code,
                Summary = $"{(taxRate.Status == TaxRateStatus.Enabled ? "启用" : "停用")}税率 {taxRate.Name}（{taxRate.Code}）",
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

        return TaxRateDtoMapper.ToTaxRateDetailDto(taxRate);
    }
}