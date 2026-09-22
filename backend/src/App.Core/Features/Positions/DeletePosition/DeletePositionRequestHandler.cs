using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Positions.DeletePosition;

/// <summary>
/// 删除岗位用例：存在性 → 无员工引用（删除保护）→ 物理删除（与审计日志同事务）
/// </summary>
public sealed class DeletePositionRequestHandler : IRequestHandler<DeletePositionRequest, object?>
{
    private readonly IPositionRepository _positionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化删除岗位用例处理器
    /// </summary>
    public DeletePositionRequestHandler(
        IPositionRepository positionRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger)
    {
        _positionRepository = positionRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理删除岗位请求
    /// </summary>
    /// <param name="request">删除请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<object?> HandleAsync(DeletePositionRequest request, CancellationToken cancellationToken = default)
    {
        var position = await _positionRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "岗位不存在");

        // 查库约束：被员工引用禁止删除（在职 / 离职员工均保留岗位引用）
        var employeeCount = await _positionRepository.CountEmployeesAsync(position.Id, cancellationToken);
        if (employeeCount > 0)
        {
            throw new BusinessException(ErrorCode.PositionInUse, $"该岗位已被 {employeeCount} 名员工引用，不可删除");
        }

        var now = DateTimeOffset.UtcNow;

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _positionRepository.DeleteAsync(position, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("code", "岗位编码", position.Code, null)
                .Add("name", "岗位名称", position.Name, null);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Position,
                Action = AuditAction.Delete,
                ResourceId = position.Id,
                ResourceNo = position.Code,
                Summary = $"删除岗位 {position.Name}（{position.Code}）",
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
