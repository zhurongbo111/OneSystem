using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.TaxRates.DeleteTaxRate;

/// <summary>
/// 删除税率用例：存在性 → 物理删除（与审计日志同事务）。
/// 税率停用承载历史语义；删除仅对未被引用的税率开放（发票 / 凭证引用由 `032` / `033` 落地后补检查）
/// </summary>
public sealed class DeleteTaxRateRequestHandler : IRequestHandler<DeleteTaxRateRequest, object?>
{
    private readonly ITaxRateRepository _taxRateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化删除税率用例处理器
    /// </summary>
    public DeleteTaxRateRequestHandler(
        ITaxRateRepository taxRateRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger)
    {
        _taxRateRepository = taxRateRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理删除税率请求
    /// </summary>
    /// <param name="request">删除请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<object?> HandleAsync(DeleteTaxRateRequest request, CancellationToken cancellationToken = default)
    {
        var taxRate = await _taxRateRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "税率不存在");

        var now = DateTimeOffset.UtcNow;

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _taxRateRepository.DeleteAsync(taxRate, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("code", "税率编码", taxRate.Code, null)
                .Add("name", "税率名称", taxRate.Name, null);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.TaxRate,
                Action = AuditAction.Delete,
                ResourceId = taxRate.Id,
                ResourceNo = taxRate.Code,
                Summary = $"删除税率 {taxRate.Name}（{taxRate.Code}）",
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

        return null;
    }
}