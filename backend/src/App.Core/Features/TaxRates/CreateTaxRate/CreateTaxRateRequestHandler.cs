using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.TaxRates.CreateTaxRate;

/// <summary>
/// 新增税率用例：编码唯一 → 名称唯一 → 落库（与审计日志同事务）
/// </summary>
public sealed class CreateTaxRateRequestHandler : IRequestHandler<CreateTaxRateRequest, TaxRateDetailDto>
{
    private readonly ITaxRateRepository _taxRateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增税率用例处理器
    /// </summary>
    public CreateTaxRateRequestHandler(
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
    /// 处理新增税率请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<TaxRateDetailDto> HandleAsync(CreateTaxRateRequest request, CancellationToken cancellationToken = default)
    {
        var code = request.Code.Trim();
        var name = request.Name.Trim();

        // 查库约束：编码全局唯一（大小写不敏感）
        if (await _taxRateRepository.ExistsByCodeAsync(code, null, cancellationToken))
        {
            throw new BusinessException(ErrorCode.TaxRateCodeExists, "税率编码已存在");
        }

        // 查库约束：名称全局唯一（大小写不敏感）
        if (await _taxRateRepository.ExistsByNameAsync(name, null, cancellationToken))
        {
            throw new BusinessException(ErrorCode.TaxRateNameExists, "税率名称已存在");
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        var taxRate = new TaxRate
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Rate = request.Rate,
            Status = (TaxRateStatus)request.Status,
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
            await _taxRateRepository.AddAsync(taxRate, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("code", "税率编码", null, taxRate.Code)
                .Add("name", "税率名称", null, taxRate.Name)
                .Add("rate", "税率", null, AuditSummary.Rate(taxRate.Rate))
                .Add("status", "状态", null, AuditText.TaxRateStatus(taxRate.Status))
                .Add("remark", "备注", null, taxRate.Remark);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.TaxRate,
                Action = AuditAction.Create,
                ResourceId = taxRate.Id,
                ResourceNo = taxRate.Code,
                Summary = $"新增税率 {taxRate.Name}（{taxRate.Code}）",
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