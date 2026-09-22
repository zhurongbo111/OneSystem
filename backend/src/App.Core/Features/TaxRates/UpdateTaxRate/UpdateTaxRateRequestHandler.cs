using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.TaxRates.UpdateTaxRate;

/// <summary>
/// 编辑税率用例：存在性 → 编码唯一 → 名称唯一 → 更新（与审计日志同事务）
/// </summary>
public sealed class UpdateTaxRateRequestHandler : IRequestHandler<UpdateTaxRateRequest, TaxRateDetailDto>
{
    private readonly ITaxRateRepository _taxRateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化编辑税率用例处理器
    /// </summary>
    public UpdateTaxRateRequestHandler(
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
    /// 处理编辑税率请求
    /// </summary>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<TaxRateDetailDto> HandleAsync(UpdateTaxRateRequest request, CancellationToken cancellationToken = default)
    {
        var taxRate = await _taxRateRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "税率不存在");

        var code = request.Code.Trim();
        var name = request.Name.Trim();

        // 查库约束：编码全局唯一（排除自身）
        if (await _taxRateRepository.ExistsByCodeAsync(code, taxRate.Id, cancellationToken))
        {
            throw new BusinessException(ErrorCode.TaxRateCodeExists, "税率编码已存在");
        }

        // 查库约束：名称全局唯一（排除自身）
        if (await _taxRateRepository.ExistsByNameAsync(name, taxRate.Id, cancellationToken))
        {
            throw new BusinessException(ErrorCode.TaxRateNameExists, "税率名称已存在");
        }

        var beforeCode = taxRate.Code;
        var beforeName = taxRate.Name;
        var beforeRate = taxRate.Rate;
        var beforeStatus = taxRate.Status;
        var beforeRemark = taxRate.Remark;

        var now = DateTimeOffset.UtcNow;
        taxRate.Code = code;
        taxRate.Name = name;
        taxRate.Rate = request.Rate;
        taxRate.Status = (TaxRateStatus)request.Status;
        taxRate.Remark = NullIfWhiteSpace(request.Remark);
        taxRate.UpdatedAt = now;
        taxRate.UpdatedBy = _currentUser.UserId();

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _taxRateRepository.UpdateAsync(taxRate, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("code", "税率编码", beforeCode, taxRate.Code)
                .Add("name", "税率名称", beforeName, taxRate.Name)
                .Add("rate", "税率", AuditSummary.Rate(beforeRate), AuditSummary.Rate(taxRate.Rate))
                .Add("status", "状态", AuditText.TaxRateStatus(beforeStatus), AuditText.TaxRateStatus(taxRate.Status))
                .Add("remark", "备注", beforeRemark, taxRate.Remark);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.TaxRate,
                Action = AuditAction.Update,
                ResourceId = taxRate.Id,
                ResourceNo = taxRate.Code,
                Summary = $"修改税率 {taxRate.Name}（{taxRate.Code}）",
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

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}