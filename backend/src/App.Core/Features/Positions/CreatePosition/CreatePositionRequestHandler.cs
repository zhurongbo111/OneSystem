using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Positions.CreatePosition;

/// <summary>
/// 新增岗位用例：编码唯一 → 名称唯一 → 落库（与审计日志同事务）
/// </summary>
public sealed class CreatePositionRequestHandler : IRequestHandler<CreatePositionRequest, PositionDetailDto>
{
    private readonly IPositionRepository _positionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增岗位用例处理器
    /// </summary>
    public CreatePositionRequestHandler(
        IPositionRepository positionRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _positionRepository = positionRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理新增岗位请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PositionDetailDto> HandleAsync(CreatePositionRequest request, CancellationToken cancellationToken = default)
    {
        var code = request.Code.Trim();
        var name = request.Name.Trim();

        // 查库约束：编码 / 名称全局唯一（大小写不敏感）
        if (await _positionRepository.ExistsByCodeAsync(code, null, cancellationToken))
        {
            throw new BusinessException(ErrorCode.PositionCodeExists, "岗位编码已存在");
        }

        if (await _positionRepository.ExistsByNameAsync(name, null, cancellationToken))
        {
            throw new BusinessException(ErrorCode.PositionNameExists, "岗位名称已存在");
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        var position = new Position
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Status = (PositionStatus)request.Status,
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
            await _positionRepository.AddAsync(position, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("code", "岗位编码", null, position.Code)
                .Add("name", "岗位名称", null, position.Name)
                .Add("status", "状态", null, AuditText.PositionStatus(position.Status))
                .Add("remark", "备注", null, position.Remark);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Position,
                Action = AuditAction.Create,
                ResourceId = position.Id,
                ResourceNo = position.Code,
                Summary = $"新增岗位 {position.Name}（{position.Code}）",
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

        return PositionDtoMapper.ToPositionDetailDto(position);
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
