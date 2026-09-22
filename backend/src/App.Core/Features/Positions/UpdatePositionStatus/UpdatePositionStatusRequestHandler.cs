using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Positions.UpdatePositionStatus;

/// <summary>
/// 岗位停用 / 启用用例：存在性 → 更新状态（与审计日志同事务）。
/// 停用后不参与员工选择，历史数据保留。
/// </summary>
public sealed class UpdatePositionStatusRequestHandler : IRequestHandler<UpdatePositionStatusRequest, PositionDetailDto>
{
    private readonly IPositionRepository _positionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化岗位停用 / 启用用例处理器
    /// </summary>
    public UpdatePositionStatusRequestHandler(
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
    /// 处理岗位停用 / 启用请求
    /// </summary>
    /// <param name="request">停用 / 启用请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PositionDetailDto> HandleAsync(UpdatePositionStatusRequest request, CancellationToken cancellationToken = default)
    {
        var position = await _positionRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "岗位不存在");

        var beforeStatus = position.Status;
        var now = DateTimeOffset.UtcNow;
        position.Status = (PositionStatus)request.Status;
        position.UpdatedAt = now;
        position.UpdatedBy = _currentUser.UserId();

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _positionRepository.UpdateAsync(position, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("status", "状态", AuditText.PositionStatus(beforeStatus), AuditText.PositionStatus(position.Status));
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Position,
                Action = AuditAction.StatusChange,
                ResourceId = position.Id,
                ResourceNo = position.Code,
                Summary = $"{(position.Status == PositionStatus.Enabled ? "启用" : "停用")}岗位 {position.Name}（{position.Code}）",
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
}
