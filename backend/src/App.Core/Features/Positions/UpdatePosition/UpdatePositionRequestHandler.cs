using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Positions.UpdatePosition;

/// <summary>
/// 编辑岗位用例：存在性 → 编码唯一（排除自身）→ 名称唯一（排除自身）→ 更新（与审计日志同事务）
/// </summary>
public sealed class UpdatePositionRequestHandler : IRequestHandler<UpdatePositionRequest, PositionDetailDto>
{
    private readonly IPositionRepository _positionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化编辑岗位用例处理器
    /// </summary>
    public UpdatePositionRequestHandler(
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
    /// 处理编辑岗位请求
    /// </summary>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PositionDetailDto> HandleAsync(UpdatePositionRequest request, CancellationToken cancellationToken = default)
    {
        var position = await _positionRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "岗位不存在");

        var code = request.Code.Trim();
        var name = request.Name.Trim();

        // 查库约束：编码 / 名称全局唯一（均排除自身）
        if (await _positionRepository.ExistsByCodeAsync(code, position.Id, cancellationToken))
        {
            throw new BusinessException(ErrorCode.PositionCodeExists, "岗位编码已存在");
        }

        if (await _positionRepository.ExistsByNameAsync(name, position.Id, cancellationToken))
        {
            throw new BusinessException(ErrorCode.PositionNameExists, "岗位名称已存在");
        }

        var beforeCode = position.Code;
        var beforeName = position.Name;
        var beforeStatus = position.Status;
        var beforeRemark = position.Remark;

        var now = DateTimeOffset.UtcNow;
        position.Code = code;
        position.Name = name;
        position.Status = (PositionStatus)request.Status;
        position.Remark = NullIfWhiteSpace(request.Remark);
        position.UpdatedAt = now;
        position.UpdatedBy = _currentUser.UserId();

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _positionRepository.UpdateAsync(position, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("code", "岗位编码", beforeCode, position.Code)
                .Add("name", "岗位名称", beforeName, position.Name)
                .Add("status", "状态", AuditText.PositionStatus(beforeStatus), AuditText.PositionStatus(position.Status))
                .Add("remark", "备注", beforeRemark, position.Remark);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Position,
                Action = AuditAction.Update,
                ResourceId = position.Id,
                ResourceNo = position.Code,
                Summary = $"修改岗位 {position.Name}（{position.Code}）",
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
